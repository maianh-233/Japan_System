using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
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

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISoftDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Property(ShadowColumns.IsDeleted).CurrentValue = true;
                entry.Property(ShadowColumns.DeletedAt).CurrentValue = now;
                entry.Property(ShadowColumns.DeletedBy).CurrentValue = by;
            }

            if (entry.Entity is not IAuditable) continue;
            if (entry.State == EntityState.Added)
            {
                entry.Property(ShadowColumns.CreatedAt).CurrentValue = now;
                entry.Property(ShadowColumns.CreatedBy).CurrentValue = by;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(ShadowColumns.UpdatedAt).CurrentValue = now;
                entry.Property(ShadowColumns.UpdatedBy).CurrentValue = by;
            }
        }
    }
}
