using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>
/// 1 lượt lead làm test online qua link có hạn (CRM-13): gửi → bắt đầu → nộp (tự chấm, gợi ý trình độ) → Sale ghi vào lịch sử tư vấn.
/// Bảng: crm.placement_attempts
/// </summary>
public sealed class PlacementAttempt : AggregateRoot<PlacementAttemptId>
{
    private PlacementAttempt() { }

    public PlacementTestId TestId { get; private set; }
    public LeadId LeadId { get; private set; }
    /// <summary>Mã bí mật trong link làm bài — duy nhất, ≥ 128 bit ngẫu nhiên</summary>
    public string Token { get; private set; } = null!;
    public ContactChannel Channel { get; private set; }
    public Guid SentBy { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public AttemptStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    /// <summary>Đáp án đã chọn: questionId → vị trí (-1 = bỏ trống) (jsonb) — lưu dần khi đang làm</summary>
    public Dictionary<Guid, int> Answers { get; private set; } = [];
    public int? Correct { get; private set; }
    public int? Total { get; private set; }
    /// <summary>Kết quả theo kỹ năng (jsonb)</summary>
    public Dictionary<JapaneseSkill, SkillScore> BySkill { get; private set; } = [];
    public string? SuggestedLevel { get; private set; }
    public string? SuggestedCourse { get; private set; }
    /// <summary>Đã ghi kết quả vào lịch sử tư vấn của lead</summary>
    public bool Logged { get; private set; }

    public bool IsGraded => Total is not null;

    /// <summary>CRM-13 bước 1 — gửi link bài test ĐANG DÙNG, hiệu lực 1 → 30 ngày. Token do Application sinh ngẫu nhiên.</summary>
    public static Result<PlacementAttempt> Send(PlacementTest test, LeadId leadId, string token, ContactChannel channel, Guid sentBy,
        DateTimeOffset sentAt, int validDays)
    {
        if (test.Status != PlacementTestStatus.Active) return PlacementTestErrors.NotActive;
        if (validDays is < 1 or > PlacementAttemptErrors.MaxValidDays) return PlacementAttemptErrors.InvalidValidity;
        if (token.Length < PlacementAttemptErrors.MinTokenLength)
            throw new ArgumentException("Token làm bài phải ≥ 128 bit ngẫu nhiên", nameof(token));
        return new PlacementAttempt
        {
            Id = PlacementAttemptId.New(), TestId = test.Id, LeadId = leadId, Token = token, Channel = channel, SentBy = sentBy,
            SentAt = sentAt, ExpiresAt = sentAt.AddDays(validDays), Status = AttemptStatus.Sent,
        };
    }

    /// <summary>Lead bắt đầu làm. Quá hạn → chuyển Hết hạn (E1); đã bắt đầu / đã nộp → từ chối (E3, mở lại link thì tiếp tục — E2).</summary>
    public Result Start(DateTimeOffset at)
    {
        if (Status == AttemptStatus.Expired) return PlacementAttemptErrors.Expired;
        if (Status != AttemptStatus.Sent) return PlacementAttemptErrors.Started;
        if (at > ExpiresAt)
        {
            Status = AttemptStatus.Expired;
            return PlacementAttemptErrors.Expired;
        }
        (Status, StartedAt) = (AttemptStatus.InProgress, at);
        return Result.Success();
    }

    /// <summary>Số giây còn lại của bài đang làm (E2: tính từ lúc bắt đầu); hết giờ → 0</summary>
    public int RemainingSeconds(int durationMinutes, DateTimeOffset now) =>
        StartedAt is { } s ? Math.Max(0, (int)(s.AddMinutes(durationMinutes) - now).TotalSeconds) : durationMinutes * 60;

    /// <summary>Lưu dần đáp án khi đang làm (mạng rớt vẫn giữ — job hết hạn chấm phần đã làm)</summary>
    public Result SaveAnswers(IReadOnlyDictionary<Guid, int> answers)
    {
        if (Status != AttemptStatus.InProgress) return PlacementAttemptErrors.NotInProgress;
        Answers = new Dictionary<Guid, int>(answers);
        return Result.Success();
    }

    /// <summary>Nộp bài (hoặc hết giờ tự nộp) → chấm + gợi ý theo thang của đề → Đã nộp</summary>
    public Result Submit(IReadOnlyDictionary<Guid, int> answers, PlacementScore score, PlacementBand? suggestion, DateTimeOffset at)
    {
        if (Status != AttemptStatus.InProgress) return PlacementAttemptErrors.NotInProgress;
        Answers = new Dictionary<Guid, int>(answers);
        Apply(score, suggestion);
        (SubmittedAt, Status) = (at, AttemptStatus.Done);
        return Result.Success();
    }

    /// <summary>
    /// CRM-13.E7 — job hằng giờ: lượt Đã gửi / Đang làm quá hạn → Hết hạn. Đang làm thì chấm phần đã lưu (<paramref name="gradeSaved"/>).
    /// Trả true nếu có đổi.
    /// </summary>
    public bool ExpireIfDue(DateTimeOffset now, Func<IReadOnlyDictionary<Guid, int>, (PlacementScore Score, PlacementBand? Band)> gradeSaved)
    {
        if (now <= ExpiresAt || Status is not (AttemptStatus.Sent or AttemptStatus.InProgress)) return false;
        if (Status == AttemptStatus.InProgress)
        {
            var (score, band) = gradeSaved(Answers);
            Apply(score, band);
            SubmittedAt = now;
        }
        Status = AttemptStatus.Expired;
        return true;
    }

    /// <summary>CRM-13 bước 4 — Sale ghi kết quả vào lịch sử tư vấn (1 lần)</summary>
    public Result MarkLogged()
    {
        if (!IsGraded) return PlacementAttemptErrors.NotGraded;
        if (Logged) return PlacementAttemptErrors.AlreadyLogged;
        Logged = true;
        return Result.Success();
    }

    private void Apply(PlacementScore score, PlacementBand? suggestion)
    {
        (Correct, Total, BySkill) = (score.Correct, score.Total, new Dictionary<JapaneseSkill, SkillScore>(score.BySkill));
        (SuggestedLevel, SuggestedCourse) = (suggestion?.Level, string.IsNullOrWhiteSpace(suggestion?.Course) ? null : suggestion.Course);
    }
}
