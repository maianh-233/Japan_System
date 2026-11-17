using JPCenter.Application.Common.Auditing;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Ghi nhật ký từ hàng đợi vào <c>system.audit_logs</c> theo lô.</summary>
internal sealed class AuditLogSink(SystemDbContext db) : IAuditSink
{
    public async Task WriteAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct)
    {
        foreach (var e in entries)
        {
            db.AuditLogs.Add(AuditLog.Create(e.At, e.ActorId, e.ActorName, e.ActorRole, e.BranchId is { } b ? new BranchId(b) : null,
                e.Category, e.Severity, e.Action, e.Target, e.Detail,
                e.Changes.Select(c => new AuditChange(c.Field, c.Before, c.After)).ToList(), e.Ip, e.Device, e.TraceId));
        }
        await db.SaveChangesAsync(ct);
    }
}
