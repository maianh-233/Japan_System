using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>
/// Ứng viên: Ứng tuyển → Sàng lọc → Phỏng vấn → Offer → Nhận việc / Loại. Bảng: hr.candidates (+ candidate_notes)
/// </summary>
public sealed class Candidate : AggregateRoot<CandidateId>, IAuditable, ISoftDeletable
{
    private readonly List<CandidateNote> _notes = [];

    private Candidate() { }

    public JobId JobId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    /// <summary>Nguồn ứng viên</summary>
    public string Source { get; private set; } = null!;
    public CandidateStage Stage { get; private set; }
    public DateOnly AppliedAt { get; private set; }
    /// <summary>File CV trong kho</summary>
    public string? CvKey { get; private set; }
    public string? CvName { get; private set; }
    /// <summary>Trình độ tiếng Nhật (GV)</summary>
    public string? Japanese { get; private set; }
    public Money? ExpectedSalary { get; private set; }
    public DateTimeOffset? InterviewAt { get; private set; }
    public Guid? InterviewerId { get; private set; }
    public InterviewMode? InterviewMode { get; private set; }
    public decimal? InterviewScore { get; private set; }
    public string? InterviewNote { get; private set; }
    public Money? OfferSalary { get; private set; }
    public DateOnly? OfferStartDate { get; private set; }
    public DateTimeOffset? OfferSentAt { get; private set; }
    public bool? OfferAccepted { get; private set; }
    public string? RejectReason { get; private set; }
    public IReadOnlyList<CandidateNote> Notes => _notes;

    public static Candidate Apply(JobId jobId, string fullName, string phone, string email, string source, DateOnly appliedAt, string? japanese,
        Money? expectedSalary) =>
        new()
        {
            Id = CandidateId.New(), JobId = jobId, FullName = fullName, Phone = phone, Email = email, Source = source, AppliedAt = appliedAt,
            Japanese = japanese, ExpectedSalary = expectedSalary, Stage = CandidateStage.Applied,
        };

    public void AttachCv(string key, string name) => (CvKey, CvName) = (key, name);
    public Result Screen() => MoveTo(CandidateStage.Screening);

    public Result ScheduleInterview(DateTimeOffset at, Guid interviewerId, InterviewMode mode)
    {
        (InterviewAt, InterviewerId, InterviewMode) = (at, interviewerId, mode);
        return MoveTo(CandidateStage.Interview);
    }

    public void ScoreInterview(decimal score, string? note) => (InterviewScore, InterviewNote) = (score, note);

    public Result SendOffer(Money salary, DateOnly startDate, DateTimeOffset at)
    {
        (OfferSalary, OfferStartDate, OfferSentAt) = (salary, startDate, at);
        return MoveTo(CandidateStage.Offer);
    }

    public Result RespondOffer(bool accepted)
    {
        if (Stage != CandidateStage.Offer) return Error.Conflict("hr.candidate.no_offer", "Ứng viên chưa được gửi offer");
        OfferAccepted = accepted;
        return accepted ? MoveTo(CandidateStage.Hired) : Reject("Từ chối offer");
    }

    public Result Reject(string reason)
    {
        if (Stage is CandidateStage.Hired) return Error.Conflict("hr.candidate.hired", "Ứng viên đã nhận việc");
        (Stage, RejectReason) = (CandidateStage.Rejected, reason);
        return Result.Success();
    }

    public void AddNote(string text, Guid by, string byName, DateTimeOffset at) => _notes.Add(new CandidateNote(text, by, byName, at));

    private Result MoveTo(CandidateStage stage)
    {
        if (Stage is CandidateStage.Hired or CandidateStage.Rejected) return Error.Conflict("hr.candidate.closed", "Hồ sơ ứng viên đã đóng");
        Stage = stage;
        return Result.Success();
    }
}
