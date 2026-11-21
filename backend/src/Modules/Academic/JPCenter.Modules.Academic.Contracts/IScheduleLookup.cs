using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>
/// Phòng + lịch buổi học cho module khác — Crm kiểm trùng lịch hẹn tư vấn / test (CRM-14.E2 → E4). Giờ là giờ địa phương (Việt Nam).
/// </summary>
public interface IScheduleLookup
{
    /// <summary>Phòng (kể cả bảo trì / ngừng dùng); đã xóa → null</summary>
    Task<RoomSummary?> RoomAsync(Guid roomId, CancellationToken ct);

    /// <summary>Phòng của các chi nhánh (ô chọn phòng khi đặt lịch)</summary>
    Task<IReadOnlyDictionary<Guid, RoomSummary>> RoomsAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct);

    /// <summary>
    /// Buổi học chưa hủy trong [<paramref name="from"/>, <paramref name="to"/>] dùng phòng <paramref name="roomId"/> HOẶC do
    /// <paramref name="teacherId"/> dạy / trợ giảng (phòng, GV đã tính buổi đổi riêng).
    /// </summary>
    Task<IReadOnlyList<BusySession>> BusyAsync(DateOnly from, DateOnly to, Guid? roomId, Guid? teacherId, CancellationToken ct);
}
