using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Báo cáo buổi của GV khi "Kết thúc buổi học" (owned trong class_sessions, cột report_*)</summary>
public sealed record SessionReport(
    string Content,
    string? Homework,
    string? Issues,
    LessonCoverage Coverage,
    List<ChapterId> CoveredChapterIds,
    DateTimeOffset EndedAt,
    Guid AuthorId,
    DateTimeOffset UpdatedAt);
