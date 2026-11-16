using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>
/// Đăng ký thi JLPT qua trung tâm: hồ sơ → lệ phí → nộp hội đồng → phiếu dự thi → kết quả → chứng chỉ gốc.
/// Bảng: students.jlpt_registrations
/// </summary>
public sealed class JlptRegistration : AggregateRoot<JlptRegistrationId>, IBranchScoped, IAuditable
{
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
    public Money Fee { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    /// <summary>Số báo danh</summary>
    public string? AdmitNo { get; private set; }
    /// <summary>Điểm từng phần theo cấu trúc đề của cấp độ</summary>
    public List<int> Scores { get; private set; } = [];
    public bool? Passed { get; private set; }
    /// <summary>Chứng chỉ gốc do hội đồng thi cấp (owned, cột cert_*)</summary>
    public JlptCertificate? Certificate { get; private set; }
    public string? Note { get; private set; }

    public static JlptRegistration Create(StudentId studentId, JlptSessionId sessionId, BranchId branchId, JlptLevel level, string venue, Money fee) =>
        new()
        {
            Id = JlptRegistrationId.New(), StudentId = studentId, SessionId = sessionId, BranchId = branchId, Level = level, Venue = venue,
            Fee = fee, Status = JlptRegistrationStatus.Registered,
        };

    public void SetDocuments(bool photo, bool idCard, bool form) => (DocPhoto, DocIdCard, DocForm) = (photo, idCard, form);

    public Result MarkPaid(DateTimeOffset at) => Move(JlptRegistrationStatus.Registered, JlptRegistrationStatus.Paid, () => PaidAt = at);

    public Result MarkSubmitted()
    {
        if (!(DocPhoto && DocIdCard && DocForm)) return Error.Conflict("students.jlpt.docs_missing", "Hồ sơ chưa đủ giấy tờ");
        return Move(JlptRegistrationStatus.Paid, JlptRegistrationStatus.Submitted, null);
    }

    public Result SetAdmit(string admitNo) => Move(JlptRegistrationStatus.Submitted, JlptRegistrationStatus.Admit, () => AdmitNo = admitNo);

    public Result RecordResult(List<int> scores, bool passed) =>
        Move(JlptRegistrationStatus.Admit, JlptRegistrationStatus.Done, () => (Scores, Passed) = (scores, passed));

    public Result MarkAbsent() => Move(JlptRegistrationStatus.Admit, JlptRegistrationStatus.Absent, null);

    public Result Cancel(string? note)
    {
        if (Status is JlptRegistrationStatus.Done or JlptRegistrationStatus.Absent) return JlptErrors.InvalidStatus;
        (Status, Note) = (JlptRegistrationStatus.Cancelled, note ?? Note);
        return Result.Success();
    }

    public Result ReceiveCertificate(string no, DateTimeOffset at, Guid by)
    {
        if (Passed != true) return Error.Conflict("students.jlpt.not_passed", "Chỉ lượt thi đỗ mới có chứng chỉ");
        Certificate = new JlptCertificate(no, at, by, null, null, null);
        return Result.Success();
    }

    public Result HandCertificate(DateTimeOffset at, Guid by, string handedTo)
    {
        if (Certificate is null) return Error.Conflict("students.jlpt.cert_not_received", "Chứng chỉ gốc chưa về trung tâm");
        Certificate = Certificate with { HandedAt = at, HandedBy = by, HandedTo = handedTo };
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
