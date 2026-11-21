using System.Text.Json;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Common;

/// <summary>
/// Cầu nối Ngân hàng câu hỏi (Academic.Contracts <see cref="IQuestionPicker"/>): bốc câu đã duyệt cho từng phần (CRM-12 bước 2),
/// chấm bài theo đáp án + kỹ năng của câu, ghi lượt làm cho thống kê câu hỏi (NH-10).
/// </summary>
public sealed class PlacementBank(IQuestionPicker picker)
{
    public static string LevelText(JlptLevel level) => level.ToString();

    public static Result<JlptLevel> ParseLevel(string level) =>
        Enum.TryParse<JlptLevel>(level, true, out var l) && l != JlptLevel.Beginner ? l : PlacementTestErrors.InvalidSection;

    public static string SkillText(JapaneseSkill skill) => JsonNamingPolicy.SnakeCaseLower.ConvertName(skill.ToString());

    /// <summary>Bốc ngẫu nhiên đủ số câu từng phần; thiếu → lỗi liệt kê từng phần (E2)</summary>
    public async Task<Result<List<Guid>>> DrawAsync(IReadOnlyList<PlacementSection> sections, CancellationToken ct)
    {
        if (sections.Count == 0) return PlacementTestErrors.NoSection;
        var picked = new List<Guid>();
        var missing = new List<string>();
        foreach (var s in sections)
        {
            var pick = await picker.PickAsync(new QuestionPickCriteria(LevelText(s.Level), [SkillText(s.Skill)], null, null, [], s.Count, picked), ct);
            if (pick.Missing > 0)
                missing.Add($"{s.Level} · {PlacementDraw.SkillLabel(s.Skill)}: cần {s.Count}, chỉ có {pick.Questions.Count} câu đã duyệt");
            picked.AddRange(pick.Questions.Select(q => q.Id));
        }
        return missing.Count > 0 ? PlacementTestErrors.NotEnoughQuestions(missing) : picked;
    }

    public Task<IReadOnlyList<PickedQuestion>> QuestionsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) => picker.GetAsync(ids, ct);

    /// <summary>Chấm theo đề cố định → điểm + mốc gợi ý; <paramref name="record"/> = ghi lượt làm vào thống kê câu hỏi</summary>
    public async Task<(PlacementScore Score, PlacementBand? Band)> GradeAsync(PlacementTest test, IReadOnlyDictionary<Guid, int> answers, bool record,
        CancellationToken ct)
    {
        var questions = await picker.GetAsync(test.QuestionIds, ct);
        var correct = await picker.AnswersAsync(test.QuestionIds, ct);
        var keys = questions.Where(q => correct.ContainsKey(q.Id)).ToDictionary(q => q.Id,
            q => new AnswerKey(Enum.Parse<JapaneseSkill>(q.Skill.Replace("_", ""), true), correct[q.Id]));
        var score = PlacementScore.Grade(test.QuestionIds, keys, answers);
        if (record)
            await picker.RecordAnswersAsync(test.QuestionIds.Where(keys.ContainsKey)
                .Select(id => new QuestionAnswer(id, answers.TryGetValue(id, out var a) ? a : -1)).ToList(), ct);
        return (score, test.Suggest(score.Percent));
    }
}
