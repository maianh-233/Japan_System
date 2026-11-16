using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>1 phần của bài test: bốc Count câu theo trình độ + kỹ năng</summary>
public sealed record PlacementSection(JlptLevel Level, JapaneseSkill Skill, int Count);
