using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>1 lượt lead làm test online qua link có hạn. Bảng: crm.placement_attempts</summary>
public sealed class PlacementAttempt : AggregateRoot<PlacementAttemptId>
{
    private PlacementAttempt() { }

    public PlacementTestId TestId { get; private set; }
    public LeadId LeadId { get; private set; }
    /// <summary>Mã bí mật trong link làm bài — duy nhất</summary>
    public string Token { get; private set; } = null!;
    public ContactChannel Channel { get; private set; }
    public Guid SentBy { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public AttemptStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    /// <summary>Đáp án đã chọn: questionId → vị trí (-1 = bỏ trống) (jsonb)</summary>
    public Dictionary<Guid, int> Answers { get; private set; } = [];
    public int? Correct { get; private set; }
    public int? Total { get; private set; }
    /// <summary>Kết quả theo kỹ năng (jsonb)</summary>
    public Dictionary<JapaneseSkill, SkillScore> BySkill { get; private set; } = [];
    public string? SuggestedLevel { get; private set; }
    public string? SuggestedCourse { get; private set; }
    /// <summary>Đã ghi kết quả vào lịch sử tư vấn của lead</summary>
    public bool Logged { get; private set; }

    public static PlacementAttempt Send(PlacementTestId testId, LeadId leadId, string token, ContactChannel channel, Guid sentBy,
        DateTimeOffset sentAt, DateTimeOffset expiresAt) =>
        new()
        {
            Id = PlacementAttemptId.New(), TestId = testId, LeadId = leadId, Token = token, Channel = channel, SentBy = sentBy,
            SentAt = sentAt, ExpiresAt = expiresAt, Status = AttemptStatus.Sent,
        };

    public Result Start(DateTimeOffset at)
    {
        if (at > ExpiresAt) { Status = AttemptStatus.Expired; return Error.Conflict("crm.placement_attempt.expired", "Link làm bài đã hết hạn"); }
        if (Status != AttemptStatus.Sent) return Error.Conflict("crm.placement_attempt.started", "Bài test đã được làm");
        (Status, StartedAt) = (AttemptStatus.InProgress, at);
        return Result.Success();
    }

    public Result Submit(Dictionary<Guid, int> answers, int correct, int total, Dictionary<JapaneseSkill, SkillScore> bySkill,
        PlacementBand? suggestion, DateTimeOffset at)
    {
        if (Status != AttemptStatus.InProgress) return Error.Conflict("crm.placement_attempt.not_in_progress", "Bài test chưa bắt đầu hoặc đã nộp");
        (Answers, Correct, Total, BySkill, SubmittedAt, Status) = (answers, correct, total, bySkill, at, AttemptStatus.Done);
        (SuggestedLevel, SuggestedCourse) = (suggestion?.Level, suggestion?.Course);
        return Result.Success();
    }

    public void MarkLogged() => Logged = true;
}
