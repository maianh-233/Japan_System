using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.AuditLogs.Common;

/// <summary>Bộ lọc NK-01 (dùng chung cho xem, xuất). Ngày theo giờ Việt Nam; không chọn ngày = 30 ngày gần nhất.</summary>
/// <param name="Q">Tìm trong hành động, đối tượng, chi tiết, người, IP</param>
public sealed record AuditLogFilter(
    Guid? BranchId = null,
    AuditCategory? Category = null,
    AuditSeverity? Severity = null,
    Guid? ActorId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Q = null);
