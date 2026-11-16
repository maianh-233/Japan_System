namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Sự cố của GV giữa buổi + cách xử lý (owned trong class_sessions, cột incident_*)</summary>
public sealed record SessionIncident(
    TimeOnly At,
    IncidentReason Reason,
    string? Detail,
    IncidentAction Action,
    Guid? TakeoverId,
    bool Notified,
    Guid ReportedBy,
    DateTimeOffset ReportedAt,
    IncidentOutcome? Outcome = null,
    DateTimeOffset? OutcomeAt = null,
    Guid? OutcomeTeacherId = null,
    Guid? OutcomeBy = null);
