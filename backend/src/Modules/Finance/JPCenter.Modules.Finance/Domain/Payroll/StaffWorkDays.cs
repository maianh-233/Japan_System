namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// Công tháng của 1 NV — đọc từ bảng công Hr (giai đoạn 21), chỉ đếm từ ngày vào làm (TC-14.E1).
/// <paramref name="HasTimesheet"/> = false → chưa có dữ liệu chấm công: tạm lấy công chuẩn (TC-14.E2).
/// </summary>
/// <param name="Worked">Ngày có đi làm (kể cả muộn / về sớm / quên check-out)</param>
/// <param name="PaidLeave">Nghỉ có lương + công tác</param>
/// <param name="NoCheckOut">Ngày quên check-out</param>
public sealed record StaffWorkDays(int Worked, int PaidLeave, int UnpaidLeave, int Absent, int Late, int NoCheckOut, bool HasTimesheet)
{
    public static readonly StaffWorkDays NoTimesheet = new(0, 0, 0, 0, 0, 0, false);
}
