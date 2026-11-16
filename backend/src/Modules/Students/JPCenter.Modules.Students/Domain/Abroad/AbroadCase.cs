using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>
/// Hồ sơ du học / XKLĐ: Tư vấn → Giấy tờ → Phỏng vấn → COE → Visa → Xuất cảnh. Bảng: students.abroad_cases
/// (+ abroad_documents, abroad_notes)
/// </summary>
public sealed class AbroadCase : AggregateRoot<AbroadCaseId>, IBranchScoped, IAuditable, ISoftDeletable
{
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

    public static AbroadCase Create(StudentId studentId, BranchId branchId, AbroadProgram program, string partner, string destination,
        string? job, string intake, JlptLevel? jlptRequired, Money feeTotal, Guid assigneeId, IEnumerable<string> documentNames)
    {
        var c = new AbroadCase
        {
            Id = AbroadCaseId.New(), StudentId = studentId, BranchId = branchId, Program = program, Partner = partner, Destination = destination,
            Job = job, Intake = intake, JlptRequired = jlptRequired, FeeTotal = feeTotal, FeePaid = Money.Zero, AssigneeId = assigneeId,
            Stage = AbroadStage.Consult,
        };
        c._documents.AddRange(documentNames.Select(n => new AbroadDocument(n)));
        return c;
    }

    public Result MoveTo(AbroadStage stage)
    {
        if (CancelledAt is not null) return Error.Conflict("students.abroad.cancelled", "Hồ sơ đã hủy");
        Stage = stage;
        return Result.Success();
    }

    public void SetDocumentState(Guid documentId, DocumentState state) => _documents.FirstOrDefault(d => d.Id == documentId)?.SetState(state);
    public void RecordInterview(DateOnly date, InterviewResult? result) => (InterviewDate, InterviewResult) = (date, result);
    public void RecordCoe(DateOnly? submittedAt, DateOnly? grantedAt) => (CoeSubmittedAt, CoeGrantedAt) = (submittedAt, grantedAt);
    public void RecordVisa(DateOnly? appliedAt, DateOnly? issuedAt) => (VisaAppliedAt, VisaIssuedAt) = (appliedAt, issuedAt);
    public void SetFlight(DateOnly? date) => FlightDate = date;
    public void RecordPayment(Money amount) => FeePaid += amount;
    public void AddNote(string text, Guid by, string byName, DateTimeOffset at) => _notes.Add(new AbroadNote(text, by, byName, at));
    public void Cancel(string reason, DateTimeOffset at) => (CancelReason, CancelledAt) = (reason, at);
}
