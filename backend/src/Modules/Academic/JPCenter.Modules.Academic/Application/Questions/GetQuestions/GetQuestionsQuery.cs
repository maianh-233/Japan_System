using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.GetQuestions;

/// <summary>
/// NH-01 — danh sách câu hỏi theo bộ lọc, mới sửa trước. Trả đủ (không phân trang): ngân hàng của 1 trung tâm cỡ vài nghìn câu,
/// trang lọc / tìm / thống kê tại chỗ như bản mock.
/// </summary>
/// <param name="Q">Tìm trong mã, đề bài, đoạn văn</param>
public sealed record GetQuestionsQuery(JlptLevel? Level, JapaneseSkill? Skill, QuestionFormat? Format, QuestionDifficulty? Difficulty,
    QuestionStatus? Status, Guid? CurriculumId, Guid? LessonId, string? Tag, Guid? AuthorId, string? Q) : IQuery<IReadOnlyList<QuestionDto>>;

public sealed class GetQuestionsHandler(QuestionBank bank) : IQueryHandler<GetQuestionsQuery, IReadOnlyList<QuestionDto>>
{
    public async Task<Result<IReadOnlyList<QuestionDto>>> Handle(GetQuestionsQuery f, CancellationToken ct)
    {
        var q = bank.Visible().AsNoTracking();
        if (f.Level is { } level) q = q.Where(x => x.Level == level);
        if (f.Skill is { } skill) q = q.Where(x => x.Skill == skill);
        if (f.Format is { } format) q = q.Where(x => x.Format == format);
        if (f.Difficulty is { } difficulty) q = q.Where(x => x.Difficulty == difficulty);
        if (f.Status is { } status) q = q.Where(x => x.Status == status);
        if (f.CurriculumId is { } cid) q = q.Where(x => x.CurriculumId == new CurriculumId(cid));
        if (f.LessonId is { } lid)
        {
            var lesson = new LessonId(lid);
            q = q.Where(x => x.LessonIds.Contains(lesson));
        }
        if (!string.IsNullOrWhiteSpace(f.Tag)) q = q.Where(x => x.Tags.Contains(f.Tag.Trim()));
        if (f.AuthorId is { } author) q = q.Where(x => x.AuthorId == author);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var pattern = $"%{f.Q.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            q = q.Where(x => EF.Functions.ILike(x.Code, pattern) || EF.Functions.ILike(x.Stem, pattern)
                || (x.Passage != null && EF.Functions.ILike(x.Passage, pattern)));
        }

        var list = await q.OrderByDescending(x => EF.Property<DateTimeOffset?>(x, "UpdatedAt") ?? EF.Property<DateTimeOffset>(x, "CreatedAt"))
            .ThenByDescending(x => x.Code).ToListAsync(ct);
        return Result.Success(await bank.ToDtosAsync(list, ct));
    }
}
