using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.Common;

/// <summary>
/// Bốc đề quiz bài tập từ Ngân hàng (chỉ câu ĐÃ DUYỆT — quy tắc vàng 1), giống <c>pickQuiz</c> ở frontend:
/// ưu tiên câu gắn đúng bài học → cùng trình độ lớp → thấp hơn 1 bậc (ND-01.E4). Trình độ lớp = cấp độ lộ trình (không có → N5).
/// </summary>
public sealed class QuizDrawer(IAcademicDbContext db, IQuestionPicker picker)
{
    public static readonly Error LessonInvalid = Error.Validation("academic.assignment.lesson_invalid", "Bài học không thuộc lộ trình của lớp");

    /// <summary>Trình độ + bài học của lộ trình lớp</summary>
    public async Task<(JlptLevel Level, HashSet<LessonId> Lessons)> CurriculumOfAsync(Class cls, CancellationToken ct)
    {
        if (cls.CurriculumId is not { } cid) return (JlptLevel.N5, []);
        var c = await db.Curricula.AsNoTracking().AsSplitQuery().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (c is null) return (JlptLevel.N5, []);
        return (c.Level == JlptLevel.Beginner ? JlptLevel.N5 : c.Level, c.Lessons.Select(l => l.Id).ToHashSet());
    }

    /// <summary>ND-01.E4 — số câu đã duyệt khớp đúng tiêu chí (bài học + kỹ năng + độ khó, trình độ lớp)</summary>
    public async Task<int> CountExactAsync(JlptLevel level, QuizConfig quiz, CancellationToken ct)
    {
        var lessons = quiz.LessonIds;
        var skills = quiz.Skills;
        var q = db.Questions.AsNoTracking().Where(x => x.Status == QuestionStatus.Approved && x.Level == level && skills.Contains(x.Skill));
        if (Difficulty(quiz.Difficulty) is { } d) q = q.Where(x => x.Difficulty == d);
        var candidates = await q.Select(x => x.LessonIds).ToListAsync(ct);
        return candidates.Count(ls => ls.Any(lessons.Contains));
    }

    /// <summary>Bốc đề: bài học → cùng trình độ → thấp hơn 1 bậc. Trả câu (không đáp án) theo thứ tự đã bốc.</summary>
    public async Task<IReadOnlyList<PickedQuestion>> DrawAsync(JlptLevel level, QuizConfig quiz, CancellationToken ct)
    {
        var skills = quiz.Skills.Select(QuestionBank.SkillText).ToList();
        var difficulty = Difficulty(quiz.Difficulty) is { } d ? QuestionBank.Text(d) : null;
        var picked = new List<PickedQuestion>();

        async Task Take(JlptLevel lv, IReadOnlyList<Guid> lessons)
        {
            var missing = quiz.QuestionCount - picked.Count;
            if (missing <= 0) return;
            var r = await picker.PickAsync(new QuestionPickCriteria(LevelText.Of(lv), skills, difficulty, null, lessons, missing,
                picked.Select(p => p.Id).ToList()), ct);
            picked.AddRange(r.Questions);
        }

        await Take(level, quiz.LessonIds.Select(l => l.Value).ToList());
        await Take(level, []);
        if (level > JlptLevel.N5) await Take(level - 1, []);
        return picked;
    }

    private static QuestionDifficulty? Difficulty(QuizDifficulty d) => d switch
    {
        QuizDifficulty.Easy => QuestionDifficulty.Easy,
        QuizDifficulty.Medium => QuestionDifficulty.Medium,
        QuizDifficulty.Hard => QuestionDifficulty.Hard,
        _ => null,
    };
}
