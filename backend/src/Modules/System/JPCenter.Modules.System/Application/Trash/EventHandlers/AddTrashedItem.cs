using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Trash.EventHandlers;

/// <summary>Module khác xóa mềm 1 bản ghi → thêm mục Thùng rác (hạn giữ theo Cài đặt chung). Chạy lại không tạo trùng (UQ kind + entity_id).</summary>
public sealed class AddTrashedItem(ISystemDbContext db, SettingsStore settings) : IIntegrationEventHandler<ItemTrashed>
{
    public async Task Handle(ItemTrashed e, CancellationToken ct)
    {
        if (await db.TrashItems.AnyAsync(t => t.Kind == e.Kind && t.EntityId == e.EntityId, ct)) return;

        var days = (await settings.GetAsync(ct)).Safety.TrashDays;
        var item = TrashItem.Create(e.Kind, e.EntityId, e.Label, e.Detail, e.BranchId is { } b ? new BranchId(b) : null, e.DeletedBy, e.DeletedAt, days);
        db.TrashItems.Add(item.Value); // days luôn hợp lệ (đã kiểm khi lưu cài đặt)
        await db.SaveChangesAsync(ct);
    }
}
