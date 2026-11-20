using JPCenter.Modules.System.Application.AuditLogs.Common;

namespace JPCenter.Modules.System.Application.AuditLogs.ExportAuditLogs;

/// <summary>Dữ liệu xuất nhật ký (NK-02)</summary>
/// <param name="Truncated">Có nhiều hơn <see cref="ExportAuditLogsHandler.MaxRows"/> dòng — chỉ trả phần mới nhất</param>
public sealed record AuditExportDto(IReadOnlyList<AuditLogDto> Items, int Total, bool Truncated);
