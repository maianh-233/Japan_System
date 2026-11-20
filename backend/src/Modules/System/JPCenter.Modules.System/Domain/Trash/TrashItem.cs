using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Trash;

/// <summary>
/// Mục trong Thùng rác — trỏ tới bản ghi đã xóa mềm ở module khác. Bảng: system.trash_items.
/// Khôi phục = gửi lệnh cho module sở hữu bỏ cờ is_deleted. Quá hạn → job xóa cứng.
/// </summary>
public sealed class TrashItem : Entity<TrashItemId>
{
    public const int MinKeepDays = 1;
    public const int MaxKeepDays = 365;

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
    /// <summary>Xóa hẳn sau thời điểm này (deletedAt + safety.trashDays lúc xóa — đổi cài đặt sau không đổi hạn, TR-04)</summary>
    public DateTimeOffset PurgeAfter { get; private set; }

    public static Result<TrashItem> Create(string kind, Guid entityId, string label, string? detail, BranchId? branchId,
        Guid deletedBy, DateTimeOffset deletedAt, int keepDays)
    {
        if (keepDays is < MinKeepDays or > MaxKeepDays) return TrashErrors.KeepDaysInvalid;
        return new TrashItem
        {
            Id = TrashItemId.New(), Kind = kind, EntityId = entityId, Label = label, Detail = detail, BranchId = branchId,
            DeletedBy = deletedBy, DeletedAt = deletedAt, PurgeAfter = deletedAt.AddDays(keepDays),
        };
    }

    /// <summary>Đã quá hạn giữ — không khôi phục được nữa, chờ job xóa hẳn (TR-02.E1, TR-04)</summary>
    public bool IsExpired(DateTimeOffset now) => now >= PurgeAfter;

    /// <summary>Còn bao nhiêu ngày trước khi bị xóa hẳn (làm tròn lên, 0 = đã hết hạn) — hiện ở trang Thùng rác</summary>
    public int DaysLeft(DateTimeOffset now) => IsExpired(now) ? 0 : (int)Math.Ceiling((PurgeAfter - now).TotalDays);

    /// <summary>Kiểm tra trước khi khôi phục (TR-02)</summary>
    public Result EnsureRestorable(DateTimeOffset now) => IsExpired(now) ? TrashErrors.Expired : Result.Success();
}
