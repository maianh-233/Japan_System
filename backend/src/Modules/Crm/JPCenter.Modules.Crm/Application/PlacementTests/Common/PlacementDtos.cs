using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Common;

/// <summary>1 phần đề — khớp <c>PlacementSection</c> frontend (<c>level</c> "N5" … "N1")</summary>
public sealed record SectionDto(string Level, JapaneseSkill Skill, int Count);

public sealed record BandDto(decimal MinPct, string Level, string Course);

/// <summary>Bài test — khớp <c>PlacementTest</c> frontend (+ authorId, số lượt làm, phiên bản)</summary>
public sealed record PlacementTestDto(Guid Id, string Code, string Name, string? Description, IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<Guid> QuestionIds, int DurationMin, IReadOnlyList<BandDto> Bands, PlacementTestStatus Status, Guid AuthorId, string Author,
    DateTimeOffset CreatedAt, int Attempts, uint Version);

/// <summary>Lượt làm — khớp <c>PlacementAttempt</c> frontend (+ tên lead / bài, % điểm)</summary>
public sealed record PlacementAttemptDto(Guid Id, Guid TestId, string TestName, Guid LeadId, string LeadName, string Token, string Link,
    ContactChannel Channel, Guid SentById, string SentBy, DateTimeOffset SentAt, DateTimeOffset ExpiresAt, AttemptStatus Status,
    DateTimeOffset? StartedAt, DateTimeOffset? SubmittedAt, IReadOnlyDictionary<Guid, int> Answers, int? Correct, int? Total, decimal? Percent,
    IReadOnlyDictionary<JapaneseSkill, SkillScore> BySkill, string? SuggestedLevel, string? SuggestedCourse, bool Logged);

/// <summary>Câu trong đề cho trang làm bài công khai — KHÔNG có đáp án (CRM-13.E5)</summary>
public sealed record PublicQuestionDto(Guid Id, string Level, string Skill, string Format, string Stem, string? Passage, IReadOnlyList<string> Options);

/// <summary>Trang làm bài <c>/t/{token}</c>: thông tin bài, trạng thái, đồng hồ, đề (khi đang làm), điểm tổng (khi xong)</summary>
public sealed record PublicAttemptDto(string TestName, string? Description, int DurationMin, int QuestionCount, AttemptStatus Status,
    DateTimeOffset ExpiresAt, int RemainingSeconds, IReadOnlyList<PublicQuestionDto> Questions, IReadOnlyDictionary<Guid, int> Answers,
    int? Correct, int? Total);
