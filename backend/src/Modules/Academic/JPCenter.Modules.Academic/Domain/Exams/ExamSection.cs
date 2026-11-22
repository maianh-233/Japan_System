using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>1 phần thi (jsonb): tên, kỹ năng, điểm mỗi câu, các câu đã bốc từ ngân hàng. Điểm tối đa = số câu × điểm mỗi câu.</summary>
public sealed record ExamSection(string Name, JapaneseSkill? Skill, decimal Points, List<QuestionId> QuestionIds)
{
    public decimal Max => Points * QuestionIds.Count;
}
