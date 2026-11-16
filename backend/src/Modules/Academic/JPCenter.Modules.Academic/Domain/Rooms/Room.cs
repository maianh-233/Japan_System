using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Rooms;

/// <summary>
/// Phòng học của 1 chi nhánh. Lịch sử dụng / lấp đầy / trùng lịch SUY RA từ buổi học. Bảng: academic.rooms, UQ (branch_id, name)
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

    public static Result<Room> Create(BranchId branchId, string name, string? floor, RoomType type, int capacity, decimal? area,
        List<Equipment> equipment, string? note)
    {
        if (capacity <= 0) return Error.Validation("academic.room.invalid_capacity", "Sức chứa phải lớn hơn 0");
        return new Room
        {
            Id = RoomId.New(), BranchId = branchId, Name = name.Trim(), Floor = floor, Type = type, Capacity = capacity, Area = area,
            Equipment = equipment, Note = note, Status = RoomStatus.Active,
        };
    }

    public void StartMaintenance(string reason, DateOnly? until) => (Status, MaintenanceReason, MaintenanceUntil) = (RoomStatus.Maintenance, reason, until);

    public void Reopen() => (Status, MaintenanceReason, MaintenanceUntil) = (RoomStatus.Active, null, null);

    /// <param name="hasOpenClasses">Còn lớp đang mở dùng phòng</param>
    public Result Deactivate(bool hasOpenClasses)
    {
        if (hasOpenClasses) return Error.Conflict("academic.room.in_use", "Còn lớp đang dùng phòng này");
        Status = RoomStatus.Inactive;
        return Result.Success();
    }
}
