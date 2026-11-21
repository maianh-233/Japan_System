using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Public;

/// <summary>
/// Dùng chung cho trang làm bài công khai <c>/t/{token}</c> (CRM-13, không đăng nhập): tìm lượt theo token, dựng trang —
/// đề KHÔNG có đáp án (E5), kết quả chỉ điểm tổng.
/// </summary>
public sealed class PublicAttempts(ICrmDbContext db, PlacementBank bank)
{
    public async Task<(PlacementAttempt Attempt, PlacementTest Test)?> FindAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < PlacementAttemptErrors.MinTokenLength) return null;
        var attempt = await db.PlacementAttempts.FirstOrDefaultAsync(a => a.Token == token, ct);
        if (attempt is null) return null;
        var test = await db.PlacementTests.IgnoreQueryFilters().FirstAsync(t => t.Id == attempt.TestId, ct);
        return (attempt, test);
    }

    public async Task<PublicAttemptDto> ToDtoAsync(PlacementAttempt a, PlacementTest test, DateTimeOffset now, CancellationToken ct)
    {
        IReadOnlyList<PublicQuestionDto> questions = [];
        if (a.Status == AttemptStatus.InProgress)
            questions = (await bank.QuestionsAsync(test.QuestionIds, ct))
                .Select(q => new PublicQuestionDto(q.Id, q.Level, q.Skill, q.Format, q.Stem, q.Passage, q.Options)).ToList();
        var done = a.Status is AttemptStatus.Done or AttemptStatus.Expired && a.IsGraded;
        return new PublicAttemptDto(test.Name, test.Description, test.DurationMinutes, test.QuestionIds.Count, a.Status, a.ExpiresAt,
            a.RemainingSeconds(test.DurationMinutes, now), questions, a.Status == AttemptStatus.InProgress ? a.Answers : new Dictionary<Guid, int>(),
            done ? a.Correct : null, done ? a.Total : null);
    }

    /// <summary>Chỉ nhận đáp án cho câu trong đề, vị trí -1 … 3</summary>
    public static Dictionary<Guid, int> Clean(PlacementTest test, IReadOnlyDictionary<Guid, int>? answers) =>
        (answers ?? new Dictionary<Guid, int>()).Where(kv => test.QuestionIds.Contains(kv.Key) && kv.Value is >= -1 and <= 3)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

    public static readonly Error NotFound = Error.NotFound("crm.placement_attempt.not_found", "Link làm bài không đúng");
}
