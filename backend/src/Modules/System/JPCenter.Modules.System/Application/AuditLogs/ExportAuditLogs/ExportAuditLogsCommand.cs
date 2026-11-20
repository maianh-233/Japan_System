using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.AuditLogs.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.AuditLogs.ExportAuditLogs;

/// <summary>
/// NK-02 — lấy toàn bộ dòng theo bộ lọc (tối đa <see cref="ExportAuditLogsHandler.MaxRows"/>) để frontend ghi file .xlsx (<c>lib/spreadsheet.ts</c> — backend không thêm
/// thư viện Excel). Là command vì bản thân việc xuất được ghi nhật ký.
/// </summary>
public sealed record ExportAuditLogsCommand(AuditLogFilter Filter) : ICommand<AuditExportDto>;

public sealed class ExportAuditLogsHandler(AuditLogReader reader, IAuditTrail audit) : ICommandHandler<ExportAuditLogsCommand, AuditExportDto>
{
    public const int MaxRows = 10_000;

    public async Task<Result<AuditExportDto>> Handle(ExportAuditLogsCommand cmd, CancellationToken ct)
    {
        var filtered = reader.Apply(cmd.Filter);
        if (filtered.IsFailure) return filtered.Error;

        var total = await filtered.Value.CountAsync(ct);
        var rows = await filtered.Value.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(MaxRows).ToListAsync(ct);

        audit.Record(AuditCategory.Export, AuditSeverity.Warning, "Xuất nhật ký hệ thống", $"{rows.Count} dòng",
            total > MaxRows ? $"Có {total} dòng, chỉ xuất {MaxRows} dòng mới nhất" : null);
        return new AuditExportDto(rows.Select(AuditLogReader.ToDto).ToList(), total, total > MaxRows);
    }
}
