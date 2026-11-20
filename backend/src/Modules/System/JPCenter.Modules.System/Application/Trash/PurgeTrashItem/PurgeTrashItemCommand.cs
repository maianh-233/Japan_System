using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Trash.Common;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Trash.PurgeTrashItem;

/// <summary>TR-03 — xóa vĩnh viễn 1 mục: module sở hữu xóa cứng bản ghi, rồi bỏ mục. Loại chưa có module nhận → chỉ bỏ mục.</summary>
public sealed record PurgeTrashItemCommand(Guid Id) : ICommand;

public sealed class PurgeTrashItemHandler(ISystemDbContext db, TrashBin bin, IAuditTrail audit) : ICommandHandler<PurgeTrashItemCommand>
{
    public async Task<Result> Handle(PurgeTrashItemCommand cmd, CancellationToken ct)
    {
        var item = await bin.FindAsync(cmd.Id, ct);
        if (item is null) return TrashErrors.NotFound;

        if (bin.HandlerFor(item.Kind) is { } handler) await handler.PurgeAsync(item.EntityId, ct);
        db.TrashItems.Remove(item);
        await db.SaveChangesAsync(ct);

        audit.Record(AuditCategory.Data, AuditSeverity.Critical, "Xóa vĩnh viễn", item.Label, item.Detail, branchId: item.BranchId?.Value);
        return Result.Success();
    }
}
