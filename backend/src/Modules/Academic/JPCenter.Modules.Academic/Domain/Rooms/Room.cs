using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Rooms;

/// <summary>
/// Phòng học của 1 chi nhánh. Lịch sử dụng / lấp đầy / trùng lịch SUY RA từ buổi học.
/// active xếp lớp được · maintenance không xếp lớp MỚI (có hạn) · inactive ngừng (chỉ khi không còn lớp đang mở dùng).
/// Bảng: academic.rooms, UQ (branch_id, name)
/// </summary>
public sealed class Room : AggregateRoot<RoomId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private Room() { }

    public BranchId BranchId { get; private set; }
    /// <summary>Tên hiển thị, duy nhất trong chi nhánh — vd P.301</summary>
    public string Name { get; private set; } = null!;
    public string? Floor { get; private set; }
    public RoomType Type { get; private set; }
    public int Capacity { get; private set; }
    /// <summary>Diện tích m²</summary>
    public decimal? Area { get; private set; }
    public List<Equipment> Equipment { get; private set; } = [];
    public RoomStatus Status { get; private set; }
    public string? MaintenanceReason { get; private set; }
    public DateOnly? MaintenanceUntil { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Xếp lớp / buổi mới vào được không</summary>
    public bool IsUsable => Status == RoomStatus.Active;

    public static Result<Room> Create(BranchId branchId, string name, string? floor, RoomType type, int capacity, decimal? area,
        List<Equipment> equipment, string? note)
    {
        var room = new Room { Id = RoomId.New(), BranchId = branchId, Status = RoomStatus.Active };
        var set = room.Update(name, floor, type, capacity, area, equipment, note, largestClass: 0);
        return set.IsFailure ? set.Error : room;
    }

    /// <param name="largestClass">Sĩ số tối đa lớn nhất của các lớp đang mở dùng phòng — sức chứa không được nhỏ hơn</param>
    public Result Update(string name, string? floor, RoomType type, int capacity, decimal? area, List<Equipment> equipment, string? note,
        int largestClass)
    {
        if (string.IsNullOrWhiteSpace(name)) return RoomErrors.NameRequired;
        if (capacity <= 0) return RoomErrors.InvalidCapacity;
        if (capacity < largestClass) return RoomErrors.CapacityBelowClass(largestClass);
        if (area is <= 0) return RoomErrors.InvalidArea;
        Name = name.Trim();
        Floor = Clean(floor);
        (Type, Capacity, Area) = (type, capacity, area);
        Equipment = [.. equipment.Distinct()];
        Note = Clean(note);
        return Result.Success();
    }

    public Result StartMaintenance(string reason, DateOnly? until)
    {
        if (Status == RoomStatus.Inactive) return RoomErrors.Inactive;
        if (string.IsNullOrWhiteSpace(reason)) return RoomErrors.MaintenanceReason;
        (Status, MaintenanceReason, MaintenanceUntil) = (RoomStatus.Maintenance, reason.Trim(), until);
        return Result.Success();
    }

    /// <summary>Hết bảo trì / dùng lại phòng đã ngừng</summary>
    public void Reopen() => (Status, MaintenanceReason, MaintenanceUntil) = (RoomStatus.Active, null, null);

    /// <param name="hasOpenClasses">Còn lớp đang mở dùng phòng</param>
    public Result Deactivate(bool hasOpenClasses)
    {
        if (hasOpenClasses) return RoomErrors.InUse;
        (Status, MaintenanceReason, MaintenanceUntil) = (RoomStatus.Inactive, null, null);
        return Result.Success();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
