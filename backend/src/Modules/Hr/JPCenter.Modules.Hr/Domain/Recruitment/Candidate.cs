using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>
/// Ứng viên (TD-02 → TD-04): Ứng tuyển → Sàng lọc → Phỏng vấn → Offer → Nhận việc / Loại. Kéo lùi được; Nhận việc / Loại là đóng hồ sơ.
/// Bảng: hr.candidates (+ candidate_notes)
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
    public DateTimeOffset AppliedAt { get; private set; }
    /// <summary>File CV trong kho</summary>
    public string? CvKey { get; private set; }
    public string? CvName { get; private set; }
    /// <summary>Trình độ tiếng Nhật (GV)</summary>
    public string? Japanese { get; private set; }
    public Money? ExpectedSalary { get; private set; }
    public DateTimeOffset? InterviewAt { get; private set; }
    /// <summary>Người phỏng vấn (Hr.Employee)</summary>
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

    public bool IsClosed => Stage is CandidateStage.Hired or CandidateStage.Rejected;

    public static Result<Candidate> Apply(JobId jobId, string fullName, string phone, string email, string source, DateTimeOffset appliedAt,
        string? japanese, Money? expectedSalary)
    {
        var c = new Candidate { Id = CandidateId.New(), JobId = jobId, AppliedAt = appliedAt, Stage = CandidateStage.Applied };
        var set = c.UpdateInfo(fullName, phone, email, source, japanese, expectedSalary);
        return set.IsFailure ? set.Error : c;
    }

    public Result UpdateInfo(string fullName, string phone, string email, string source, string? japanese, Money? expectedSalary)
    {
        if (new[] { fullName, phone, email, source }.Any(string.IsNullOrWhiteSpace)) return RecruitmentErrors.CandidateInfo;
        (FullName, Phone, Email, Source, Japanese, ExpectedSalary) =
            (fullName.Trim(), phone.Trim(), email.Trim().ToLowerInvariant(), source.Trim(), Clean(japanese), expectedSalary);
        return Result.Success();
    }

    public void AttachCv(string key, string name) => (CvKey, CvName) = (key, name);

    /// <summary>TD-02 — kéo thả sang Ứng tuyển / Sàng lọc, hoặc lùi về Phỏng vấn / Offer khi đã có thông tin</summary>
    public Result MoveTo(CandidateStage stage)
    {
        if (IsClosed) return RecruitmentErrors.Closed;
        var ready = stage switch
        {
            CandidateStage.Applied or CandidateStage.Screening => true,
            CandidateStage.Interview => InterviewAt is not null,
            CandidateStage.Offer => OfferSentAt is not null,
            _ => false,
        };
        if (!ready) return RecruitmentErrors.StageAction;
        Stage = stage;
        return Result.Success();
    }

    /// <summary>TD-03 — hẹn / sửa lịch phỏng vấn (chuyển sang Phỏng vấn nếu đang ở giai đoạn trước)</summary>
    public Result ScheduleInterview(DateTimeOffset at, Guid interviewerId, InterviewMode mode, decimal? score, string? note)
    {
        if (IsClosed) return RecruitmentErrors.Closed;
        if (score is < 0 or > 10) return RecruitmentErrors.InvalidScore;
        (InterviewAt, InterviewerId, InterviewMode, InterviewScore, InterviewNote) = (at, interviewerId, mode, score, Clean(note));
        if (Stage is CandidateStage.Applied or CandidateStage.Screening) Stage = CandidateStage.Interview;
        return Result.Success();
    }

    /// <summary>TD-03 — gửi offer</summary>
    public Result SendOffer(Money salary, DateOnly startDate, DateTimeOffset at, DateOnly today)
    {
        if (IsClosed) return RecruitmentErrors.Closed;
        if (salary <= Money.Zero || startDate < today) return RecruitmentErrors.OfferInvalid;
        (OfferSalary, OfferStartDate, OfferSentAt, OfferAccepted) = (salary, startDate, at, null);
        Stage = CandidateStage.Offer;
        return Result.Success();
    }

    /// <summary>TD-03 — ứng viên từ chối offer → Loại</summary>
    public Result DeclineOffer()
    {
        if (Stage != CandidateStage.Offer) return RecruitmentErrors.NoOffer;
        OfferAccepted = false;
        (Stage, RejectReason) = (CandidateStage.Rejected, "Từ chối offer");
        return Result.Success();
    }

    /// <summary>TD-04 — nhận việc (chỉ từ Offer)</summary>
    public Result Hire()
    {
        if (Stage != CandidateStage.Offer) return IsClosed ? RecruitmentErrors.Closed : RecruitmentErrors.NoOffer;
        OfferAccepted = true;
        Stage = CandidateStage.Hired;
        return Result.Success();
    }

    /// <summary>TD-04 — loại (lý do bắt buộc)</summary>
    public Result Reject(string reason)
    {
        if (Stage == CandidateStage.Hired) return RecruitmentErrors.Hired;
        if (Stage == CandidateStage.Rejected) return RecruitmentErrors.Closed;
        if (string.IsNullOrWhiteSpace(reason)) return RecruitmentErrors.RejectReason;
        (Stage, RejectReason) = (CandidateStage.Rejected, reason.Trim());
        return Result.Success();
    }

    public Result AddNote(string text, Guid by, string byName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(text)) return RecruitmentErrors.NoteRequired;
        _notes.Add(new CandidateNote(text.Trim(), by, byName, at));
        return Result.Success();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
