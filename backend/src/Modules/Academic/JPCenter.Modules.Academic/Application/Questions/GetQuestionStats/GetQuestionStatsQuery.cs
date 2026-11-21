using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.GetQuestionStats;

/// <summary>NH-01 bước 3 — thẻ đầu trang theo phạm vi quyền.</summary>
public sealed record GetQuestionStatsQuery : IQuery<QuestionStatsSummary>;

/// <param name="Hard">Đã duyệt, ≥ <see cref="GetQuestionStatsHandler.HardMinAttempts"/> lượt làm, tỷ lệ đúng &lt; <see cref="GetQuestionStatsHandler.HardBelowPct"/>%</param>
/// <param name="Unlinked">Chưa ngừng dùng mà chưa gắn bài học</param>
public sealed record QuestionStatsSummary(int Total, int Approved, int Review, int Draft, int Retired, int Hard, int Unlinked);

public sealed class GetQuestionStatsHandler(QuestionBank bank) : IQueryHandler<GetQuestionStatsQuery, QuestionStatsSummary>
{
    public const int HardMinAttempts = 20;
    public const int HardBelowPct = 40;

    public async Task<Result<QuestionStatsSummary>> Handle(GetQuestionStatsQuery query, CancellationToken ct)
    {
        var rows = await bank.Visible().AsNoTracking()
            .Select(q => new { q.Status, q.AttemptCount, q.CorrectCount, Linked = q.LessonIds.Count > 0 })
            .ToListAsync(ct);
        return new QuestionStatsSummary(
            rows.Count,
            rows.Count(r => r.Status == QuestionStatus.Approved),
            rows.Count(r => r.Status == QuestionStatus.Review),
            rows.Count(r => r.Status == QuestionStatus.Draft),
            rows.Count(r => r.Status == QuestionStatus.Retired),
            rows.Count(r => r.Status == QuestionStatus.Approved && r.AttemptCount >= HardMinAttempts
                && r.CorrectCount * 100 < HardBelowPct * r.AttemptCount),
            rows.Count(r => r.Status != QuestionStatus.Retired && !r.Linked));
    }
}
