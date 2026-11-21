using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Questions.Common;

/// <summary>Nội dung câu hỏi client gửi khi thêm / sửa / nhập Excel (NH-02, NH-03, NH-08) — tên trường giống form frontend.</summary>
public sealed record QuestionInput(
    JlptLevel Level,
    JapaneseSkill Skill,
    QuestionFormat Format,
    QuestionDifficulty Difficulty,
    string Stem,
    string? Passage,
    string? Transcript,
    List<string>? Options,
    int Answer,
    string? Explanation,
    Guid? CurriculumId,
    List<Guid>? LessonIds,
    List<string>? Tags)
{
    public QuestionContent ToContent() => new(Level, Skill, Format, Difficulty, Stem, Passage, Transcript, Options ?? [], Answer, Explanation,
        CurriculumId is { } c ? new CurriculumId(c) : null, (LessonIds ?? []).Select(l => new LessonId(l)).ToList(), Tags ?? []);
}
