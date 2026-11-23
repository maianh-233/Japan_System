using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Contracts.Dtos;

/// <summary>1 ca đã xếp cho nhân viên làm theo ca. <paramref name="Short"/>: "S" · "C" · "T"</summary>
/// <param name="EmployeeId">Hr.Employee</param>
public sealed record ScheduledShift(Guid Id, Guid EmployeeId, Guid BranchId, DateOnly Date, ShiftKey Shift, TimeOnly From, TimeOnly To, string Short);
