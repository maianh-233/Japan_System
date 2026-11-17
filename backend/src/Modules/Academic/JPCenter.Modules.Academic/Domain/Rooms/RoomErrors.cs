using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Rooms;

public static class RoomErrors
{
    public static readonly Error NameRequired = Error.Validation("academic.room.name_required", "Nhập tên phòng");
    public static readonly Error InvalidCapacity = Error.Validation("academic.room.invalid_capacity", "Sức chứa phải lớn hơn 0");
    public static readonly Error InvalidArea = Error.Validation("academic.room.invalid_area", "Diện tích phải lớn hơn 0");
    public static readonly Error MaintenanceReason = Error.Validation("academic.room.maintenance_reason", "Cần ghi lý do bảo trì");
    public static readonly Error Inactive = Error.Conflict("academic.room.inactive", "Phòng đã ngừng sử dụng");
    public static readonly Error InUse = Error.Conflict("academic.room.in_use", "Còn lớp đang dùng phòng này");
    public static readonly Error Unavailable = Error.Conflict("academic.room.unavailable", "Phòng đang bảo trì hoặc đã ngừng sử dụng");
    public static readonly Error WrongBranch = Error.Validation("academic.room.wrong_branch", "Phòng không thuộc chi nhánh của lớp");

    public static Error DuplicateName(string name) => Error.Conflict("academic.room.duplicate_name", $"Chi nhánh đã có phòng {name}");

    public static Error CapacityBelowClass(int max) =>
        Error.Validation("academic.room.invalid_capacity", $"Sức chứa không được nhỏ hơn sĩ số tối đa của lớp đang dùng phòng ({max})");
}
