namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>Tên shadow property của các cột chung (database.md "Cột chung") — snake_case tự đổi thành created_at...</summary>
public static class ShadowColumns
{
    public const string CreatedAt = "CreatedAt";
    public const string CreatedBy = "CreatedBy";
    public const string UpdatedAt = "UpdatedAt";
    public const string UpdatedBy = "UpdatedBy";
    public const string IsDeleted = "IsDeleted";
    public const string DeletedAt = "DeletedAt";
    public const string DeletedBy = "DeletedBy";
    /// <summary>Cột hệ thống của PostgreSQL — concurrency token chống sửa đè</summary>
    public const string RowVersion = "xmin";

    /// <summary>Tên query filter xóa mềm — tắt riêng khi cần xem Thùng rác: <c>IgnoreQueryFilters([SoftDeleteFilter])</c>.</summary>
    public const string SoftDeleteFilter = "soft_delete";
}
