using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.TimeRecords;

namespace JPCenter.Modules.Hr.Application.Timesheets.Common;

/// <summary>Bản ghi chấm công — khớp <c>TimeRecord</c> frontend (giờ "HH:mm")</summary>
public sealed record TimeRecordDto(Guid Id, Guid EmployeeId, DateOnly Date, string? CheckIn, string? CheckOut, EditedDto? Edited);

/// <summary>Sửa tay: tên người sửa + lý do (tooltip "sửa bởi …")</summary>
public sealed record EditedDto(string By, string Reason);

/// <summary>Giờ phải làm: "HC" (hành chính) hoặc ca "S", "C+T"…</summary>
public sealed record WorkWindowDto(string From, string To, string Label);

/// <summary>1 ngày trên bảng công (CC-02, CC-03). Trạng thái tính khi đọc — không lưu.</summary>
public sealed record TimesheetDayDto(DateOnly Date, AttendanceState State, WorkWindowDto? Expected, string? CheckIn, string? CheckOut,
    int LateMin, int EarlyMin, int WorkedMin, bool Problem, Guid? RecordId, EditedDto? Edited);

/// <summary>Tổng tháng: ngày công, số lần muộn (+ tổng phút), về sớm, quên check-out, vắng không phép, ngày phép, giờ công</summary>
public sealed record TimesheetTotalsDto(int WorkDays, int Late, int LateMin, int Early, int NoOut, int Absent, int Leave, decimal Hours);

/// <summary>Nhân viên trên bảng công</summary>
public sealed record TimesheetEmployeeDto(Guid Id, string Code, string FullName, Guid BranchId, Department Department, string Position,
    bool ShiftBased, EmployeeStatus Status, decimal LeaveBalance);

/// <summary>CC-02 — bảng công tháng của tôi. <c>today</c> chỉ có khi tháng đang xem là tháng hiện tại.</summary>
public sealed record MyTimesheetDto(TimesheetEmployeeDto Employee, string Month, IReadOnlyList<TimesheetDayDto> Days, TimesheetTotalsDto Stats,
    decimal LeaveBalance, IReadOnlyList<TimesheetDayDto> Problems, TimesheetDayDto? Today);

/// <summary>CC-03 — 1 dòng lưới toàn bộ nhân viên</summary>
public sealed record TimesheetRowDto(TimesheetEmployeeDto Employee, IReadOnlyList<TimesheetDayDto> Days, TimesheetTotalsDto Totals);

/// <summary>CC-03 — lưới nhân viên × ngày</summary>
public sealed record TimesheetBoardDto(string Month, IReadOnlyList<TimesheetRowDto> Rows);
