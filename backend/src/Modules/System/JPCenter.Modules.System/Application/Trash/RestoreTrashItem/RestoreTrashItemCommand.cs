using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Trash.Common;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Trash.RestoreTrashItem;

/// <summary>TR-02 — khôi phục 1 mục: module sở hữu bỏ cờ xóa mềm, rồi bỏ mục khỏi Thùng rác.</summary>
public sealed record RestoreTrashItemCommand(Guid Id) : ICommand<RestoreTrashResponse>;

public sealed class RestoreTrashItemHandler(ISystemDbContext db, TrashBin bin, IAuditTrail audit, IClock clock)
    : ICommandHandler<RestoreTrashItemCommand, RestoreTrashResponse>
{
    public async Task<Result<RestoreTrashResponse>> Handle(RestoreTrashItemCommand cmd, CancellationToken ct)
    {
        var item = await bin.FindAsync(cmd.Id, ct);
        if (item is null) return TrashErrors.NotFound;
        if (item.EnsureRestorable(clock.UtcNow) is { IsFailure: true } expired) return expired.Error;
        if (bin.HandlerFor(item.Kind) is not { } handler) return TrashErrors.NotRestorable;

        var restored = await handler.RestoreAsync(item.EntityId, ct);
        if (restored.IsFailure) return restored.Error;

        db.TrashItems.Remove(item);
        await db.SaveChangesAsync(ct);
        if (!restored.Value.SourceFound) return TrashErrors.SourceMissing;

        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Khôi phục từ Thùng rác", item.Label, restored.Value.Note,
            branchId: item.BranchId?.Value);
        return new RestoreTrashResponse(item.Kind, item.EntityId, restored.Value.Note);
    }
}
