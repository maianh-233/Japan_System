using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>
/// Hồ sơ du học / XKLĐ (HV-17): Tư vấn → Giấy tờ → Phỏng vấn → COE → Visa → Xuất cảnh — đi từng bước, mỗi bước có điều kiện
/// (giống frontend <c>AbroadDrawer</c>): giấy tờ kiểm tra hết · đỗ phỏng vấn · đã nhận COE · đã có visa. Dừng (hủy) cần lý do, mở lại được.
/// Mỗi lần chuyển bước / dừng / mở lại tự ghi 1 ghi chú. Bảng: students.abroad_cases (+ abroad_documents, abroad_notes)
/// </summary>
public sealed partial class AbroadCase : AggregateRoot<AbroadCaseId>, IBranchScoped, IAuditable, ISoftDeletable
{
    public const int MaxText = 200;
    public const int MaxNote = 1000;

    private readonly List<AbroadDocument> _documents = [];
    private readonly List<AbroadNote> _notes = [];

    private AbroadCase() { }

    public StudentId StudentId { get; private set; }
    public BranchId BranchId { get; private set; }
    public AbroadProgram Program { get; private set; }
    /// <summary>Trường Nhật ngữ / nghiệp đoàn / công ty tiếp nhận</summary>
    public string Partner { get; private set; } = null!;
    /// <summary>Tỉnh / thành phố ở Nhật</summary>
    public string Destination { get; private set; } = null!;
    /// <summary>Ngành nghề (XKLĐ)</summary>
    public string? Job { get; private set; }
    /// <summary>Kỳ nhập học / dự kiến xuất cảnh, dạng YYYY-MM</summary>
    public string Intake { get; private set; } = null!;
    public JlptLevel? JlptRequired { get; private set; }
    public AbroadStage Stage { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateOnly? InterviewDate { get; private set; }
    public InterviewResult? InterviewResult { get; private set; }
    public DateOnly? CoeSubmittedAt { get; private set; }
    public DateOnly? CoeGrantedAt { get; private set; }
    public DateOnly? VisaAppliedAt { get; private set; }
    public DateOnly? VisaIssuedAt { get; private set; }
    public DateOnly? FlightDate { get; private set; }
    public Money FeeTotal { get; private set; }
    public Money FeePaid { get; private set; }
    /// <summary>Người phụ trách (Hr.Employee)</summary>
    public Guid AssigneeId { get; private set; }
    public IReadOnlyList<AbroadDocument> Documents => _documents;
    public IReadOnlyList<AbroadNote> Notes => _notes;

    public bool IsCancelled => CancelledAt is not null;
    public bool AllDocumentsVerified => _documents.All(d => d.State == DocumentState.Verified);

    /// <param name="documentNames">Checklist giấy tờ theo mẫu chương trình (du học / lao động — frontend <c>docTemplate</c>)</param>
    public static Result<AbroadCase> Create(StudentId studentId, BranchId branchId, AbroadCaseInfo info, IEnumerable<string> documentNames)
    {
        var c = new AbroadCase { Id = AbroadCaseId.New(), StudentId = studentId, BranchId = branchId, FeePaid = Money.Zero, Stage = AbroadStage.Consult };
        var set = c.Apply(info);
        if (set.IsFailure) return set.Error;
        c._documents.AddRange(documentNames.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct().Select(n => new AbroadDocument(n)));
        return c;
    }

    /// <summary>Sửa thông tin chung (tổng phí không nhỏ hơn số đã đóng)</summary>
    public Result Update(AbroadCaseInfo info) => IsCancelled ? AbroadErrors.Cancelled : Apply(info);

    /// <summary>Sang bước kế tiếp nếu đủ điều kiện của bước hiện tại</summary>
    public Result Advance(Guid by, string byName, DateTimeOffset at)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        var blocker = Stage switch
        {
            AbroadStage.Documents when !AllDocumentsVerified => AbroadErrors.DocsNotVerified,
            AbroadStage.Interview when InterviewResult != Abroad.InterviewResult.Pass => AbroadErrors.InterviewNotPassed,
            AbroadStage.Coe when CoeGrantedAt is null => AbroadErrors.CoeMissing,
            AbroadStage.Visa when VisaIssuedAt is null => AbroadErrors.VisaMissing,
            AbroadStage.Departed => AbroadErrors.AlreadyDeparted,
            _ => null,
        };
        if (blocker is not null) return blocker;
        Stage += 1;
        _notes.Add(new AbroadNote($"Chuyển sang bước \"{StageLabel(Stage)}\".", by, byName, at));
        return Result.Success();
    }

    public Result SetDocumentState(Guid documentId, DocumentState state)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        if (_documents.FirstOrDefault(d => d.Id == documentId) is not { } doc) return AbroadErrors.DocumentNotFound;
        doc.SetState(state);
        return Result.Success();
    }

    /// <summary>Ngày phỏng vấn + kết quả (kết quả cần có ngày). Trượt → ở lại bước Phỏng vấn, đặt lịch mới hoặc dừng (E2)</summary>
    public Result RecordInterview(DateOnly? date, InterviewResult? result)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        if (date is null && result is not null) return AbroadErrors.InterviewDateRequired;
        (InterviewDate, InterviewResult) = (date, date is null ? null : result);
        return Result.Success();
    }

    public Result RecordCoe(DateOnly? submittedAt, DateOnly? grantedAt)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        if (!ValidPair(submittedAt, grantedAt)) return AbroadErrors.InvalidDates;
        (CoeSubmittedAt, CoeGrantedAt) = (submittedAt, grantedAt);
        return Result.Success();
    }

    public Result RecordVisa(DateOnly? appliedAt, DateOnly? issuedAt)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        if (!ValidPair(appliedAt, issuedAt)) return AbroadErrors.InvalidDates;
        (VisaAppliedAt, VisaIssuedAt) = (appliedAt, issuedAt);
        return Result.Success();
    }

    public Result SetFlight(DateOnly? date)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        FlightDate = date;
        return Result.Success();
    }

    /// <summary>Ghi nhận HV đóng phí dịch vụ (không vượt tổng phí)</summary>
    public Result RecordPayment(Money amount)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        if (amount.Amount <= 0 || FeePaid + amount > FeeTotal) return AbroadErrors.InvalidPayment;
        FeePaid += amount;
        return Result.Success();
    }

    public Result Assign(Guid assigneeId)
    {
        if (assigneeId == Guid.Empty) return AbroadErrors.AssigneeRequired;
        AssigneeId = assigneeId;
        return Result.Success();
    }

    /// <summary>Ghi chú tiến trình (ghi được cả khi hồ sơ đã dừng)</summary>
    public Result AddNote(string? text, Guid by, string byName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxNote) return AbroadErrors.NoteRequired;
        _notes.Add(new AbroadNote(text.Trim(), by, byName, at));
        return Result.Success();
    }

    /// <summary>HV-17 bước 4 — dừng hồ sơ (lý do bắt buộc)</summary>
    public Result Cancel(string? reason, Guid by, string byName, DateTimeOffset at)
    {
        if (IsCancelled) return AbroadErrors.Cancelled;
        if (string.IsNullOrWhiteSpace(reason)) return AbroadErrors.ReasonRequired;
        (CancelReason, CancelledAt) = (reason.Trim(), at);
        _notes.Add(new AbroadNote($"Dừng hồ sơ: {CancelReason}", by, byName, at));
        return Result.Success();
    }

    /// <summary>Mở lại hồ sơ đã dừng — giữ nguyên bước đang dở</summary>
    public Result Reopen(Guid by, string byName, DateTimeOffset at)
    {
        if (!IsCancelled) return AbroadErrors.NotCancelled;
        (CancelReason, CancelledAt) = (null, null);
        _notes.Add(new AbroadNote("Mở lại hồ sơ.", by, byName, at));
        return Result.Success();
    }

    public static string StageLabel(AbroadStage stage) => stage switch
    {
        AbroadStage.Consult => "Tư vấn",
        AbroadStage.Documents => "Hồ sơ giấy tờ",
        AbroadStage.Interview => "Phỏng vấn",
        AbroadStage.Coe => "Chờ COE",
        AbroadStage.Visa => "Xin visa",
        _ => "Đã xuất cảnh",
    };

    private Result Apply(AbroadCaseInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Partner) || string.IsNullOrWhiteSpace(info.Destination)
            || info.Partner.Trim().Length > MaxText || info.Destination.Trim().Length > MaxText)
            return AbroadErrors.PartnerRequired;
        var intake = info.Intake?.Trim();
        if (intake is null || !IntakePattern().IsMatch(intake)) return AbroadErrors.InvalidIntake;
        if (info.AssigneeId == Guid.Empty) return AbroadErrors.AssigneeRequired;
        if (info.JlptRequired is JlptLevel.Beginner) return AbroadErrors.InvalidLevel;
        if (info.FeeTotal < FeePaid) return AbroadErrors.InvalidPayment;
        (Program, Partner, Destination, Intake, JlptRequired, FeeTotal, AssigneeId) =
            (info.Program, info.Partner.Trim(), info.Destination.Trim(), intake, info.JlptRequired, info.FeeTotal, info.AssigneeId);
        Job = string.IsNullOrWhiteSpace(info.Job) ? null : info.Job.Trim();
        return Result.Success();
    }

    private static bool ValidPair(DateOnly? submitted, DateOnly? received) =>
        received is null || (submitted is not null && received >= submitted);

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex IntakePattern();
}
