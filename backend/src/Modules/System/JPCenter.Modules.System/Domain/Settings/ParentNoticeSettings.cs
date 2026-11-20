namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Thông báo phụ huynh</summary>
/// <param name="ArrivalNotice">Báo phụ huynh khi con được điểm danh có mặt / đi muộn</param>
/// <param name="AbsentAlertMin">Quá N phút sau giờ vào lớp chưa có mặt (không có đơn nghỉ) → báo; 0 = tắt — 0 → 120</param>
/// <param name="UnderAge">Chỉ báo đến lớp / vắng cho HV dưới N tuổi; 0 = mọi HV có phụ huynh — 0 → 25</param>
/// <param name="WeeklyReport">Tự gửi báo cáo tuần</param>
/// <param name="WeeklyDay">Ngày gửi: 0 = Chủ nhật … 6 = Thứ 7</param>
/// <param name="WeeklyTime">Giờ gửi (giờ Việt Nam)</param>
public sealed record ParentNoticeSettings(bool ArrivalNotice, int AbsentAlertMin, int UnderAge, bool WeeklyReport, int WeeklyDay, TimeOnly WeeklyTime);
