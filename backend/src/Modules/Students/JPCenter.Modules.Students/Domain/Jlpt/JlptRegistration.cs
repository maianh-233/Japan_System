using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>
/// Đăng ký thi JLPT qua trung tâm (HV-16): registered → paid → submitted → admit → done | absent; hủy được trước khi có kết quả.
/// Chứng chỉ gốc (lượt đỗ): chờ về → đã về → đã phát. Bảng: students.jlpt_registrations
/// </summary>
public sealed class JlptRegistration : AggregateRoot<JlptRegistrationId>, IBranchScoped, IAuditable
{
    public const int MaxNote = 500;

    private JlptRegistration() { }

    public StudentId StudentId { get; private set; }
    public JlptSessionId SessionId { get; private set; }
    public BranchId BranchId { get; private set; }
    public JlptLevel Level { get; private set; }
    public string Venue { get; private set; } = null!;
    public JlptRegistrationStatus Status { get; private set; }
    /// <summary>Hồ sơ đã nộp: ảnh 3×4, CCCD, phiếu ĐK đã ký</summary>
    public bool DocPhoto { get; private set; }
    public bool DocIdCard { get; private set; }
    public bool DocForm { get; private set; }
    /// <summary>Lệ phí lấy theo cấp độ của kỳ thi lúc đăng ký (HV-16.E7 — thu ngoài hóa đơn học phí)</summary>
    public Money Fee { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    /// <summary>Số báo danh</summary>
    public string? AdmitNo { get; private set; }
    /// <summary>Điểm từng phần theo cấu trúc đề của cấp độ (<see cref="JlptScoring"/>)</summary>
    public List<int> Scores { get; private set; } = [];
    public bool? Passed { get; private set; }
    /// <summary>Chứng chỉ gốc do hội đồng thi cấp (owned, cột cert_*)</summary>
    public JlptCertificate? Certificate { get; private set; }
    public string? Note { get; private set; }

    public bool HasAllDocuments => DocPhoto && DocIdCard && DocForm;

    /// <summary>
    /// HV-16 bước 2 — cấp độ N5 → N1, địa điểm thuộc kỳ thi, lệ phí theo kỳ thi. Quá hạn nhận hồ sơ chỉ cảnh báo
    /// (<see cref="JlptSession.IsClosed"/> — E6); trùng HV + kỳ thi Application kiểm (<see cref="JlptErrors.Duplicate"/>).
    /// </summary>
    public static Result<JlptRegistration> Create(StudentId studentId, JlptSession session, BranchId branchId, JlptLevel level, string? venue)
    {
        if (!JlptScoring.IsExamLevel(level)) return JlptErrors.InvalidLevel;
        var place = venue?.Trim();
        if (place is null || !session.Venues.Contains(place)) return JlptErrors.InvalidVenue;
        return new JlptRegistration
        {
            Id = JlptRegistrationId.New(), StudentId = studentId, SessionId = session.Id, BranchId = branchId, Level = level, Venue = place,
            Fee = session.FeeFor(level), Status = JlptRegistrationStatus.Registered,
        };
    }

    /// <summary>Đổi cấp độ / địa điểm — chỉ trước khi nộp hội đồng; lệ phí tính lại theo cấp độ mới</summary>
    public Result ChangeExam(JlptSession session, JlptLevel level, string? venue)
    {
        if (Status is not (JlptRegistrationStatus.Registered or JlptRegistrationStatus.Paid)) return JlptErrors.InvalidStatus;
        if (!JlptScoring.IsExamLevel(level)) return JlptErrors.InvalidLevel;
        var place = venue?.Trim();
        if (place is null || !session.Venues.Contains(place)) return JlptErrors.InvalidVenue;
        (Level, Venue) = (level, place);
        if (Status == JlptRegistrationStatus.Registered) Fee = session.FeeFor(level);
        return Result.Success();
    }

    /// <summary>Checklist hồ sơ — chỉ sửa được trước khi nộp hội đồng</summary>
    public Result SetDocuments(bool photo, bool idCard, bool form)
    {
        if (Status is not (JlptRegistrationStatus.Registered or JlptRegistrationStatus.Paid)) return JlptErrors.InvalidStatus;
        (DocPhoto, DocIdCard, DocForm) = (photo, idCard, form);
        return Result.Success();
    }

    public Result MarkPaid(DateTimeOffset at) => Move(JlptRegistrationStatus.Registered, JlptRegistrationStatus.Paid, () => PaidAt = at);

    /// <summary>E2 — nộp hội đồng cần đủ 3 giấy tờ</summary>
    public Result MarkSubmitted()
    {
        if (Status == JlptRegistrationStatus.Paid && !HasAllDocuments) return JlptErrors.DocsMissing;
        return Move(JlptRegistrationStatus.Paid, JlptRegistrationStatus.Submitted, null);
    }

    public Result SetAdmit(string? admitNo)
    {
        if (string.IsNullOrWhiteSpace(admitNo)) return JlptErrors.AdmitNoRequired;
        return Move(JlptRegistrationStatus.Submitted, JlptRegistrationStatus.Admit, () => AdmitNo = admitNo.Trim());
    }

    /// <summary>Nhập điểm từng phần → tự tính đỗ / trượt theo quy chế (<see cref="JlptScoring.Judge"/>)</summary>
    public Result RecordResult(IReadOnlyList<int> scores)
    {
        if (Status != JlptRegistrationStatus.Admit) return JlptErrors.InvalidStatus;
        if (JlptScoring.Judge(Level, scores) is not { } result) return JlptErrors.InvalidScores;
        (Scores, Passed, Status) = (scores.ToList(), result.Passed, JlptRegistrationStatus.Done);
        return Result.Success();
    }

    public Result MarkAbsent() => Move(JlptRegistrationStatus.Admit, JlptRegistrationStatus.Absent, null);

    /// <summary>Hủy đăng ký — trước khi có kết quả / vắng</summary>
    public Result Cancel(string? note)
    {
        if (Status is JlptRegistrationStatus.Done or JlptRegistrationStatus.Absent or JlptRegistrationStatus.Cancelled)
            return JlptErrors.InvalidStatus;
        Status = JlptRegistrationStatus.Cancelled;
        SetNote(note);
        return Result.Success();
    }

    public void SetNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return;
        var n = note.Trim();
        Note = n.Length > MaxNote ? n[..MaxNote] : n;
    }

    /// <summary>E4 — chứng chỉ gốc về trung tâm (chỉ lượt đỗ); sửa lại số được khi chưa phát</summary>
    public Result ReceiveCertificate(string? no, DateTimeOffset at, Guid by)
    {
        if (Passed != true) return JlptErrors.NotPassed;
        if (Certificate?.HandedAt is not null) return JlptErrors.CertAlreadyHanded;
        if (string.IsNullOrWhiteSpace(no)) return JlptErrors.CertNoRequired;
        Certificate = new JlptCertificate(no.Trim(), at, by, null, null, null);
        return Result.Success();
    }

    /// <summary>E5 — phát chứng chỉ cho HV / phụ huynh (chỉ khi đã về, phát 1 lần)</summary>
    public Result HandCertificate(DateTimeOffset at, Guid by, string? handedTo)
    {
        if (Certificate is null) return JlptErrors.CertNotReceived;
        if (Certificate.HandedAt is not null) return JlptErrors.CertAlreadyHanded;
        if (string.IsNullOrWhiteSpace(handedTo)) return JlptErrors.HandedToRequired;
        Certificate = Certificate with { HandedAt = at, HandedBy = by, HandedTo = handedTo.Trim() };
        return Result.Success();
    }

    private Result Move(JlptRegistrationStatus from, JlptRegistrationStatus to, Action? apply)
    {
        if (Status != from) return JlptErrors.InvalidStatus;
        apply?.Invoke();
        Status = to;
        return Result.Success();
    }
}
