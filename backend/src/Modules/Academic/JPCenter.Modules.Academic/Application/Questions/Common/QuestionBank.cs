using System.Text.Json;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.Common;

/// <summary>
/// Dùng chung cho các use case Ngân hàng câu hỏi (user story 11):
/// phạm vi quyền (own = câu mình soạn; branch / all = mọi câu — ngân hàng không thuộc chi nhánh), kiểm tra lộ trình ↔ bài học,
/// sinh mã câu, dựng DTO kèm tên người soạn / duyệt.
/// </summary>
public sealed class QuestionBank(IAcademicDbContext db, ICurrentUser user, IEmployeeDirectory employees)
{
    public const string Page = "academic.questions";

    /// <summary>Người thao tác (Hr.Employee) — tài khoản không gắn nhân viên dùng Id tài khoản</summary>
    public Guid Me => user.Access?.EmployeeId ?? user.AccountId ?? Guid.Empty;

    /// <summary>Phạm vi <c>own</c> của 1 hành động → chỉ câu mình soạn</summary>
    public bool OwnOnly(PermissionAction action) => user.Permission(Page, action) is { Scope: PermissionScope.Own };

    public bool Can(PermissionAction action) => user.Can(Page, action);

    /// <summary>Câu được xem (NH-01.E1)</summary>
    public IQueryable<Question> Visible()
    {
        var query = db.Questions.AsQueryable();
        if (OwnOnly(PermissionAction.View))
        {
            var me = Me;
            query = query.Where(q => q.AuthorId == me);
        }
        return query;
    }

    /// <summary>Câu được sửa bằng hành động này (phạm vi own → câu mình soạn); không có / ngoài phạm vi → null (404)</summary>
    public async Task<Question?> ForUpdateAsync(Guid id, PermissionAction action, CancellationToken ct)
    {
        var q = await db.Questions.FirstOrDefaultAsync(x => x.Id == new QuestionId(id), ct);
        if (q is null || (OwnOnly(action) && q.AuthorId != Me)) return null;
        return q;
    }

    /// <summary>NH-02.E6 — lộ trình có thật, cùng cấp độ, bài học thuộc lộ trình</summary>
    public async Task<Result> CheckLessonsAsync(QuestionContent content, CancellationToken ct)
    {
        if (content.CurriculumId is not { } cid) return content.LessonIds.Count == 0 ? Result.Success() : QuestionErrors.LessonInvalid;
        var curriculum = await db.Curricula.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.Id == cid, ct);
        if (curriculum is null || curriculum.Level != content.Level) return QuestionErrors.LessonInvalid;
        var lessons = curriculum.Lessons.Select(l => l.Id).ToHashSet();
        return content.LessonIds.All(lessons.Contains) ? Result.Success() : QuestionErrors.LessonInvalid;
    }

    /// <summary>Mã câu mới "N3-GR-1024": số thứ tự tăng dần toàn ngân hàng (kể cả câu đã xóa)</summary>
    public async Task<Func<JlptLevel, JapaneseSkill, string>> CodeGeneratorAsync(CancellationToken ct)
    {
        var codes = await db.Questions.IgnoreQueryFilters().Select(q => q.Code).ToListAsync(ct);
        var next = codes.Select(c => int.TryParse(c[(c.LastIndexOf('-') + 1)..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return (level, skill) => $"{LevelText.Of(level)}-{SkillText(skill)[..2].ToUpperInvariant()}-{++next:D4}";
    }

    public async Task<IReadOnlyList<QuestionDto>> ToDtosAsync(IReadOnlyList<Question> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ids = list.Select(q => q.Id).ToList();
        var meta = await db.Questions.AsNoTracking().IgnoreQueryFilters().Where(q => ids.Contains(q.Id))
            .Select(q => new { q.Id, Version = EF.Property<uint>(q, "xmin"), Created = EF.Property<DateTimeOffset>(q, "CreatedAt"),
                Updated = EF.Property<DateTimeOffset?>(q, "UpdatedAt") })
            .ToDictionaryAsync(x => x.Id, ct);
        var people = list.Select(q => q.AuthorId).Concat(list.Where(q => q.ReviewedBy != null).Select(q => q.ReviewedBy!.Value))
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var names = await employees.FindManyAsync(people, ct);
        string Name(Guid id) => names.TryGetValue(id, out var e) ? e.FullName : "—";

        return list.Select(q =>
        {
            var m = meta.GetValueOrDefault(q.Id);
            return new QuestionDto(q.Id.Value, q.Code, LevelText.Of(q.Level), SkillText(q.Skill), Text(q.Format), Text(q.Difficulty), q.Stem, q.Passage,
                q.AudioName is { } name ? new QuestionAudioDto(name, q.AudioDurationSec ?? 0) : null, q.Transcript, q.Options, q.Answer, q.Explanation,
                q.CurriculumId?.Value, q.LessonIds.Select(l => l.Value).ToList(), q.Tags, Text(q.Status), q.AuthorId, Name(q.AuthorId),
                m?.Created ?? default, m?.Updated ?? m?.Created ?? default,
                q.ReviewedBy is { } by && q.ReviewedAt is { } at ? new QuestionReviewDto(by, Name(by), at, q.ReviewNote) : null,
                new QuestionStatsDto(q.UsedCount, q.AttemptCount, q.CorrectCount, q.AttemptCount > 0 ? q.Picks : null), m?.Version ?? 0);
        }).ToList();
    }

    public async Task<QuestionDto> ToDtoAsync(Question q, CancellationToken ct) => (await ToDtosAsync([q], ct))[0];

    public static string SkillText(JapaneseSkill skill) => Text(skill);

    public static string Text<T>(T value) where T : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
