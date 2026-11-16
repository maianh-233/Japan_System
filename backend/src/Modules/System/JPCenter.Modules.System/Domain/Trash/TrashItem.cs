using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Trash;

/// <summary>
/// Mục trong Thùng rác — trỏ tới bản ghi đã xóa mềm ở module khác. Bảng: system.trash_items.
/// Khôi phục = gửi lệnh cho module sở hữu bỏ cờ is_deleted. Quá hạn → job xóa cứng.
/// </summary>
public sealed class TrashItem : Entity<TrashItemId>
{
    private TrashItem() { }

    /// <summary>Loại dữ liệu "module.entity", vd finance.invoice, crm.message_template</summary>
    public string Kind { get; private set; } = null!;
    /// <summary>Id bản ghi gốc</summary>
    public Guid EntityId { get; private set; }
    public string Label { get; private set; } = null!;
    public string? Detail { get; private set; }
    public BranchId? BranchId { get; private set; }
    public DateTimeOffset DeletedAt { get; private set; }
    public Guid DeletedBy { get; private set; }
    /// <summary>Xóa hẳn sau thời điểm này (deletedAt + safety.trashDays)</summary>
    public DateTimeOffset PurgeAfter { get; private set; }

    public static TrashItem Create(string kind, Guid entityId, string label, string? detail, BranchId? branchId,
        Guid deletedBy, DateTimeOffset deletedAt, int keepDays) =>
        new()
        {
            Id = TrashItemId.New(), Kind = kind, EntityId = entityId, Label = label, Detail = detail, BranchId = branchId,
            DeletedBy = deletedBy, DeletedAt = deletedAt, PurgeAfter = deletedAt.AddDays(keepDays),
        };
}
