using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>
/// Trước khi lưu: tự điền created_at/by, updated_at/by (IAuditable); đổi xóa thành xóa mềm (ISoftDeletable).
/// Xóa cứng chỉ khi dọn Thùng rác — gọi <c>ExecuteDelete</c> trực tiếp, không đi qua đây.
/// </summary>
public sealed class AuditFieldsInterceptor(IClock clock, ICurrentUser user) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = clock.UtcNow;
        var by = user.AccountId;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is ISoftDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Property(ShadowColumns.IsDeleted).CurrentValue = true;
                entry.Property(ShadowColumns.DeletedAt).CurrentValue = now;
                entry.Property(ShadowColumns.DeletedBy).CurrentValue = by;
                KeepOwned(context, entry);
            }

            if (entry.Entity is not IAuditable) continue;
            if (entry.State == EntityState.Added)
            {
                // Dữ liệu mẫu đặt sẵn thời điểm tạo (SeedData.SetCreatedAt) → giữ nguyên; luồng thường không đặt → lấy giờ hiện tại
                var created = entry.Property(ShadowColumns.CreatedAt);
                if (created.CurrentValue is not DateTimeOffset preset || preset == default) created.CurrentValue = now;
                entry.Property(ShadowColumns.CreatedBy).CurrentValue = by;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(ShadowColumns.UpdatedAt).CurrentValue = now;
                entry.Property(ShadowColumns.UpdatedBy).CurrentValue = by;
            }
        }
    }

    /// <summary>
    /// Xóa gốc aggregate → EF xóa dây chuyền các con owned (bài học, chapter, tài liệu…). Gốc chỉ xóa mềm thì con phải giữ nguyên
    /// để khôi phục từ Thùng rác được đầy đủ.
    /// </summary>
    private static void KeepOwned(DbContext context, EntityEntry owner)
    {
        foreach (var nav in owner.Navigations.Where(n => n.Metadata.TargetEntityType.IsOwned()))
        {
            IEnumerable<object> children = nav.CurrentValue switch
            {
                null => [],
                System.Collections.IEnumerable items and not string => items.Cast<object>(),
                var single => [single],
            };
            foreach (var child in children)
            {
                var childEntry = context.Entry(child);
                if (childEntry.State == EntityState.Deleted) childEntry.State = EntityState.Unchanged;
                KeepOwned(context, childEntry);
            }
        }
    }
}
