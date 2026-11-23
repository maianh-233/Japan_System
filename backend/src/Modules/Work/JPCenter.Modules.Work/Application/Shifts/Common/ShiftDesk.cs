using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Shifts.Common;

/// <summary>Ca đã xếp — khớp <c>ShiftAssignment</c> frontend</summary>
public sealed record ShiftAssignmentDto(Guid Id, Guid EmployeeId, DateOnly Date, ShiftKey Shift);

/// <summary>Rảnh / bận — khớp <c>ShiftAvailability</c> frontend</summary>
public sealed record ShiftAvailabilityDto(Guid EmployeeId, DateOnly Date, ShiftKey Shift, ShiftRegistrationKind Status);

public sealed record ShiftEmployeeDto(Guid Id, string Code, string FullName, string Department, string Position, string Status);

/// <summary>Ngày nghỉ đã duyệt (XC-01.E5). <c>type</c>: leave · business_trip</summary>
public sealed record LeaveDayDto(Guid EmployeeId, DateOnly Date, string Type);

/// <summary>XC-03 — số người mỗi ca so với tối thiểu theo bộ phận; thiếu → đỏ</summary>
public sealed record CoverageDto(DateOnly Date, ShiftKey Shift, int Count, int Minimum, bool Short);

/// <summary>XC-01 — lưới xếp ca 1 tuần (T2–CN) của chi nhánh</summary>
public sealed record ShiftWeekDto(Guid BranchId, DateOnly WeekStart, IReadOnlyList<ShiftEmployeeDto> Employees, IReadOnlyList<ShiftAssignmentDto> Assignments,
    IReadOnlyList<ShiftAvailabilityDto> Registrations, IReadOnlyList<LeaveDayDto> Leaves, IReadOnlyList<CoverageDto> Coverage, bool CanEdit);

/// <summary>Nhân viên làm theo ca của chi nhánh + kiểm quyền <c>work.shifts.*</c> theo chi nhánh.</summary>
public sealed class ShiftDesk(WorkPeople people, ICurrentUser user)
{
    public const string Page = "work.shifts";
    public static readonly ShiftKey[] Shifts = [ShiftKey.Morning, ShiftKey.Afternoon, ShiftKey.Evening];

    public static readonly Error BranchNotAllowed = Error.Forbidden("work.shift.branch_not_allowed", "Chi nhánh ngoài phạm vi xếp ca của bạn");

    public bool InBranch(PermissionAction action, Guid branchId) => user.Can(Page, action) && user.InBranch(Page, action, branchId);

    /// <summary>Thứ 2 của tuần chứa ngày</summary>
    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>NV làm theo ca, chưa nghỉ việc, của chi nhánh (kể cả đang nghỉ dài hạn — để thấy ô trống)</summary>
    public async Task<IReadOnlyList<EmployeeSummary>> ShiftStaffAsync(Guid branchId, CancellationToken ct) =>
        (await people.ActiveAsync(ct)).Where(e => e.ShiftBased && !e.IsTeacher && e.BranchId == branchId).OrderBy(e => e.FullName).ToList();

    /// <summary>XC-01.E3 — NV xếp được: làm theo ca, đang làm việc (không nghỉ dài hạn / nghỉ việc)</summary>
    public static bool Assignable(EmployeeSummary? e) => e is { ShiftBased: true, IsTeacher: false, IsWorking: true };

    public static ShiftAssignmentDto ToDto(ShiftAssignment a) => new(a.Id.Value, a.EmployeeId, a.Date, a.Shift);

    public static ShiftAvailabilityDto ToDto(ShiftRegistration r) => new(r.EmployeeId, r.Date, r.Shift, r.Kind);
}
