using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>Khôi phục bản ghi xóa mềm (Thùng rác) — đọc bản ghi bằng <c>IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])</c> trước.</summary>
public static class SoftDeleteExtensions
{
    /// <summary>Bản ghi đang bị xóa mềm?</summary>
    public static bool IsSoftDeleted(this DbContext db, ISoftDeletable entity) =>
        db.Entry(entity).Property<bool>(ShadowColumns.IsDeleted).CurrentValue;

    /// <summary>Bỏ cờ xóa mềm (is_deleted = false, xóa deleted_at / deleted_by) — lưu bằng <c>SaveChangesAsync</c> như thường.</summary>
    public static void Undelete(this DbContext db, ISoftDeletable entity)
    {
        var entry = db.Entry(entity);
        entry.Property(ShadowColumns.IsDeleted).CurrentValue = false;
        entry.Property(ShadowColumns.DeletedAt).CurrentValue = null;
        entry.Property(ShadowColumns.DeletedBy).CurrentValue = null;
    }
}
