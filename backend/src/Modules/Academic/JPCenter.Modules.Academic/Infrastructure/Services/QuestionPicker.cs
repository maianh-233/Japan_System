using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IQuestionPicker"/> (NH-09, NH-10): bốc ngẫu nhiên bằng <c>random()</c> của PostgreSQL trong các câu đã duyệt.</summary>
internal sealed class QuestionPicker(AcademicDbContext db) : IQuestionPicker
{
    public const int MaxCount = 200;

    public async Task<QuestionPick> PickAsync(QuestionPickCriteria c, CancellationToken ct)
    {
        var count = Math.Clamp(c.Count, 0, MaxCount);
        if (count == 0 || !Enum.TryParse<JlptLevel>(c.Level, ignoreCase: true, out var level)) return new QuestionPick([], count);

        var q = db.Questions.Where(x => x.Status == QuestionStatus.Approved && x.Level == level);
        var skills = c.Skills.Select(s => Enum.TryParse<JapaneseSkill>(s, ignoreCase: true, out var v) ? v : (JapaneseSkill?)null)
            .OfType<JapaneseSkill>().ToList();
        if (skills.Count > 0) q = q.Where(x => skills.Contains(x.Skill));
        if (c.Difficulty is { } d && Enum.TryParse<QuestionDifficulty>(d, ignoreCase: true, out var difficulty)) q = q.Where(x => x.Difficulty == difficulty);
        if (c.CurriculumId is { } cid) q = q.Where(x => x.CurriculumId == new CurriculumId(cid));
        if (c.Exclude is { Count: > 0 } exclude)
        {
            var ids = exclude.Select(e => new QuestionId(e)).ToList();
            q = q.Where(x => !ids.Contains(x.Id));
        }

        List<Question> picked;
        if (c.LessonIds.Count > 0)
        {
            // Bài học lưu jsonb (không dịch được sang SQL) → lọc bài trong bộ nhớ trên Id + bài, rồi nạp các câu đã chọn
            var lessons = c.LessonIds.Select(l => new LessonId(l)).ToHashSet();
            var candidates = await q.Select(x => new { x.Id, x.LessonIds }).ToListAsync(ct);
            var chosen = candidates.Where(x => x.LessonIds.Any(lessons.Contains)).Select(x => x.Id).OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
            var loaded = await db.Questions.Where(x => chosen.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            picked = chosen.Where(loaded.ContainsKey).Select(id => loaded[id]).ToList();
        }
        else picked = await q.OrderBy(_ => EF.Functions.Random()).Take(count).ToListAsync(ct);
        foreach (var p in picked) p.RecordUse();
        if (picked.Count > 0) await db.SaveChangesAsync(ct);
        return new QuestionPick(picked.Select(ToPicked).ToList(), count - picked.Count);
    }

    public async Task<IReadOnlyList<PickedQuestion>> GetAsync(IReadOnlyList<Guid> questionIds, CancellationToken ct)
    {
        var ids = questionIds.Select(i => new QuestionId(i)).ToList();
        var found = await db.Questions.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id.Value, ct);
        return questionIds.Where(found.ContainsKey).Select(id => ToPicked(found[id])).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> AnswersAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken ct)
    {
        var ids = questionIds.Select(i => new QuestionId(i)).ToList();
        return await db.Questions.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id.Value, x => x.Answer, ct);
    }

    public async Task RecordAnswersAsync(IReadOnlyList<QuestionAnswer> answers, CancellationToken ct)
    {
        if (answers.Count == 0) return;
        var ids = answers.Select(a => new QuestionId(a.QuestionId)).Distinct().ToList();
        var questions = await db.Questions.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id.Value, ct);
        foreach (var a in answers)
            if (questions.TryGetValue(a.QuestionId, out var q)) q.RecordAnswer(a.Picked);
        await db.SaveChangesAsync(ct);
    }

    private static PickedQuestion ToPicked(Question q) => new(q.Id.Value, q.Code, q.Level.ToString(), QuestionBank.SkillText(q.Skill),
        QuestionBank.Text(q.Format), QuestionBank.Text(q.Difficulty), q.Stem, q.Passage, q.AudioKey, q.AudioDurationSec, q.Options);
}
