using JPCenter.Modules.Reporting.Domain.Metrics;

namespace JPCenter.Modules.Reporting.Domain.Branches;

/// <summary>Số thô của 1 chi nhánh trong kỳ, ghép từ các nguồn của module khác (BC-06).</summary>
/// <param name="BranchId">System.Branch</param>
/// <param name="Leads">Lead tạo trong kỳ (không tính lead ẩn)</param>
/// <param name="WonLeads">Trong số đó, đã chốt</param>
/// <param name="Students">HV khác nhau đang học</param>
/// <param name="Classes">Lớp đang học + đang tuyển sinh</param>
/// <param name="Seats">Tổng sĩ số tối đa của các lớp đó</param>
/// <param name="FilledSeats">HV đang học + bảo lưu của các lớp đó</param>
/// <param name="Attendance">Lượt điểm danh trong kỳ</param>
/// <param name="Collected">Thực thu cả kỳ (thu − hoàn)</param>
/// <param name="CollectedThisMonth">Thực thu tháng hiện tại</param>
/// <param name="Debt">Còn phải thu</param>
/// <param name="Overdue">Trong đó đã quá hạn</param>
public sealed record BranchFigures(Guid BranchId, string Name, int Leads, int WonLeads, int Students, int Classes, int Seats, int FilledSeats,
    AttendanceTally Attendance, decimal Collected, decimal CollectedThisMonth, decimal Debt, decimal Overdue);
