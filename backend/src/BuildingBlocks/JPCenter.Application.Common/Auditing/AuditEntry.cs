using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Auditing;

/// <summary>1 dòng nhật ký hệ thống (system.audit_logs) — dựng qua <see cref="IAuditTrail"/>, ghi nền qua <see cref="IAuditWriter"/>.</summary>
public sealed record AuditEntry(
    DateTimeOffset At,
    Guid? ActorId,
    string ActorName,
    string ActorRole,
    Guid? BranchId,
    AuditCategory Category,
    AuditSeverity Severity,
    string Action,
    string Target,
    string? Detail,
    IReadOnlyList<AuditFieldChange> Changes,
    string Ip,
    string Device,
    string? TraceId);

/// <summary>1 trường thay đổi trước → sau (giá trị bí mật đã che).</summary>
public sealed record AuditFieldChange(string Field, string? Before, string? After);
