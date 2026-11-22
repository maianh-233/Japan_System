using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>
/// Soạn đề kỳ thi (ND-04 bước 3): mỗi phần lấy câu đã chọn (kiểm: ĐÃ DUYỆT, đúng trình độ lớp, trong phạm vi bài của mốc) hoặc bốc ngẫu nhiên
/// <c>Count</c> câu đã duyệt trong phạm vi (đúng kỹ năng của phần). Thiếu câu → <c>academic.exam.not_enough_questions</c> kèm số thiếu từng phần (E2).
/// </summary>
public sealed class ExamDrafter(IAcademicDbContext db, IQuestionPicker picker)
{
    public const int MaxPerSection = 100;

    public static Error QuestionInvalid(string code) => Error.Validation("academic.exam.question_invalid",
        $"Câu {code} chưa duyệt, khác trình độ hoặc ngoài phạm vi bài của mốc thi");

    /// <param name="exclude">Câu không bốc (đổi câu khi xem trước)</param>
    public async Task<Result<List<ExamSection>>> BuildAsync(JlptLevel level, IReadOnlyList<LessonId> scope, IReadOnlyList<ExamSectionInput> input,
        IReadOnlyCollection<Guid>? exclude, CancellationToken ct)
    {
        if (input.Count == 0 || input.Any(s => (s.QuestionIds?.Count ?? s.Count ?? 0) is <= 0 or > MaxPerSection)) return ExamErrors.Sections;

        // Câu chọn sẵn: kiểm trạng thái / trình độ / phạm vi
        var given = input.SelectMany(s => s.QuestionIds ?? []).Distinct().Select(g => new QuestionId(g)).ToList();
        var found = await db.Questions.AsNoTracking().Where(q => given.Contains(q.Id)).ToDictionaryAsync(q => q.Id.Value, ct);
        foreach (var id in given)
        {
            if (!found.TryGetValue(id.Value, out var q)) return QuestionInvalid(id.Value.ToString()[..8]);
            if (q.Status != QuestionStatus.Approved || q.Level != level || (scope.Count > 0 && !q.LessonIds.Any(scope.Contains)))
                return QuestionInvalid(q.Code);
        }

        var used = new HashSet<Guid>(given.Select(g => g.Value).Concat(exclude ?? []));
        var sections = new List<ExamSection>();
        var missing = new List<(string, int)>();
        foreach (var s in input)
        {
            var name = s.Name?.Trim() ?? "";
            if (s.QuestionIds is { Count: > 0 } ids)
            {
                sections.Add(new ExamSection(name, s.Skill, s.Points, ids.Distinct().Select(g => new QuestionId(g)).ToList()));
                continue;
            }
            var count = s.Count ?? 0;
            var pick = await picker.PickAsync(new QuestionPickCriteria(LevelText.Of(level), s.Skill is { } sk ? [QuestionBank.SkillText(sk)] : [],
                null, null, scope.Select(l => l.Value).ToList(), count, used), ct);
            if (pick.Missing > 0) missing.Add((name.Length == 0 ? "Phần thi" : name, pick.Missing));
            foreach (var q in pick.Questions) used.Add(q.Id);
            sections.Add(new ExamSection(name, s.Skill, s.Points, pick.Questions.Select(q => new QuestionId(q.Id)).ToList()));
        }
        if (missing.Count > 0) return ExamErrors.NotEnoughQuestions(missing);
        return sections;
    }

    /// <summary>Đề có đáp án (xem trước, bản in thi giấy)</summary>
    public async Task<IReadOnlyList<ExamPaperSectionDto>> PaperAsync(IReadOnlyList<ExamSection> sections, CancellationToken ct)
    {
        var ids = sections.SelectMany(s => s.QuestionIds).Distinct().ToList();
        var questions = await db.Questions.AsNoTracking().IgnoreQueryFilters().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        return sections.Select(s => new ExamPaperSectionDto(s.Name, s.Skill, s.Points,
            s.QuestionIds.Where(questions.ContainsKey).Select(id => questions[id]).Select(q => new ExamQuestionDto(q.Id.Value, q.Code,
                QuestionBank.SkillText(q.Skill), QuestionBank.Text(q.Difficulty), q.Stem, q.Passage, q.Options, q.Answer, q.Explanation)).ToList()))
            .ToList();
    }
}
