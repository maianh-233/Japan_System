using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>Dữ liệu để kiểm trùng 1 lịch hẹn — Application nạp từ Crm, Academic (phòng, buổi học) và Hr (người tiếp).</summary>
/// <param name="Appointments">Lịch hẹn khác cùng khoảng thời gian (mọi trạng thái — chỉ "Đã hẹn" mới tính)</param>
/// <param name="RoomName">Tên phòng đã chọn (null = quầy, không kiểm phòng)</param>
/// <param name="RoomUnavailable">Phòng không dùng được: "bảo trì" / "ngừng dùng" (null = đang dùng)</param>
/// <param name="RoomSessions">Buổi học dùng phòng đó giao giờ</param>
/// <param name="StaffName">Tên người tiếp</param>
/// <param name="StaffOnLongLeave">Người tiếp đang nghỉ dài ngày</param>
/// <param name="StaffTeaching">Giờ dạy của người tiếp (nếu là GV) giao giờ</param>
/// <param name="LeadName">Tên lead theo Id (ghi trong thông điệp)</param>
/// <param name="ToLocal">Đổi giờ UTC → giờ Việt Nam để hiện HH:mm</param>
public sealed record ConflictContext(
    IReadOnlyList<Appointment> Appointments,
    string? RoomName,
    string? RoomUnavailable,
    IReadOnlyList<BusySlot> RoomSessions,
    string StaffName,
    bool StaffOnLongLeave,
    IReadOnlyList<BusySlot> StaffTeaching,
    Func<LeadId, string> LeadName,
    Func<DateTimeOffset, DateTime> ToLocal);
