namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>
/// Công tháng của 1 NV tính từ bảng công (CC-03) — chỉ ngày còn hiệu lực làm việc (TC-14.E1).
/// <paramref name="HasTimesheet"/> = false: tháng chưa có bản ghi chấm công nào (TC-14.E2 — Finance tạm lấy công chuẩn).
/// </summary>
/// <param name="Worked">Ngày có đi làm (kể cả muộn / về sớm / quên check-out / đang làm)</param>
/// <param name="PaidLeave">Nghỉ phép có lương + công tác (không tính Chủ nhật)</param>
/// <param name="UnpaidLeave">Nghỉ không lương (không tính Chủ nhật)</param>
/// <param name="Absent">Vắng không phép</param>
/// <param name="Late">Ngày đi muộn</param>
/// <param name="NoCheckOut">Ngày quên check-out</param>
public sealed record WorkDaysSummary(int Worked, int PaidLeave, int UnpaidLeave, int Absent, int Late, int NoCheckOut, bool HasTimesheet);
