using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Đáp án đúng + kỹ năng của 1 câu trong đề (đọc từ Ngân hàng câu hỏi lúc chấm)</summary>
public sealed record AnswerKey(JapaneseSkill Skill, int Answer);
