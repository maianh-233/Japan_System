using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Application.Trash.PurgeExpiredTrash;

/// <summary>TR-04 — job hằng đêm: mục quá hạn → module sở hữu xóa hẳn, bỏ mục. Lỗi ở 1 mục không chặn các mục khác (thử lại đêm sau).</summary>
public sealed record PurgeExpiredTrashCommand : ICommand<int>;

public sealed class PurgeExpiredTrashHandler(ISystemDbContext db, IEnumerable<ITrashHandler> handlers, IAuditTrail audit, IClock clock,
    ILogger<PurgeExpiredTrashHandler> logger) : ICommandHandler<PurgeExpiredTrashCommand, int>
{
    private const int Batch = 500;
    private static readonly AuditActor SystemActor = new(null, "Hệ thống", "Hệ thống");

    public async Task<Result<int>> Handle(PurgeExpiredTrashCommand cmd, CancellationToken ct)
    {
        var byKind = handlers.ToDictionary(h => h.Kind);
        var now = clock.UtcNow;
        var expired = await db.TrashItems.Where(t => t.PurgeAfter <= now).OrderBy(t => t.PurgeAfter).Take(Batch).ToListAsync(ct);

        var purged = 0;
        foreach (var item in expired)
        {
            try
            {
                if (byKind.TryGetValue(item.Kind, out var handler)) await handler.PurgeAsync(item.EntityId, ct);
                else logger.LogWarning("Thùng rác: loại {Kind} chưa có module xóa hẳn — chỉ bỏ mục {Id}", item.Kind, item.Id.Value);
                db.TrashItems.Remove(item);
                await db.SaveChangesAsync(ct);
                audit.Record(AuditCategory.Data, AuditSeverity.Critical, "Xóa vĩnh viễn (hết hạn Thùng rác)", item.Label, item.Detail, SystemActor,
                    item.BranchId?.Value);
                purged++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Thùng rác: không xóa hẳn được {Kind} {EntityId}", item.Kind, item.EntityId);
            }
        }
        return purged;
    }
}
