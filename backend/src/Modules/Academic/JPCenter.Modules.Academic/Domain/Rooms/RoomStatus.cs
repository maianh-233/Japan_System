namespace JPCenter.Modules.Academic.Domain.Rooms;

/// <summary>Trạng thái phòng</summary>
public enum RoomStatus
{
    /// <summary>Đang dùng</summary>
    Active,
    /// <summary>Bảo trì (không xếp lớp mới)</summary>
    Maintenance,
    /// <summary>Ngừng sử dụng</summary>
    Inactive,
}
