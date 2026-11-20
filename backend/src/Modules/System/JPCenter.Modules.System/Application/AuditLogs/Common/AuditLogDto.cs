using JPCenter.Modules.System.Domain.AuditLogs;

namespace JPCenter.Modules.System.Application.AuditLogs.Common;

/// <summary>1 dòng nhật ký — khớp type <c>AuditLog</c> ở <c>frontend/src/features/system/types.ts</c> (<c>actor</c> = tên người làm).</summary>
/// <param name="Category">auth · permission · data · export · finance · setting · security</param>
/// <param name="Severity">info · warning · critical</param>
public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset At,
    Guid? ActorId,
    string Actor,
    string ActorRole,
    Guid? BranchId,
    string Category,
    string Severity,
    string Action,
    string Target,
    string? Detail,
    IReadOnlyList<AuditChange> Changes,
    string Ip,
    string Device);
