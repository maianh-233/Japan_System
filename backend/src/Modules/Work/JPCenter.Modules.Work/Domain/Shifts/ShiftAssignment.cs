using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Ca làm được xếp cho nhân viên "làm theo ca" (XC-01). Bảng: work.shift_assignments, UQ (employee_id, date, shift)</summary>
public sealed class ShiftAssignment : AggregateRoot<ShiftAssignmentId>, IBranchScoped, IAuditable
{
    private ShiftAssignment() { }

    /// <summary>Hr.Employee</summary>
    public Guid EmployeeId { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateOnly Date { get; private set; }
    public ShiftKey Shift { get; private set; }

    /// <param name="busy">NV đã báo bận ca này (XC-01.E2)</param>
    public static Result<ShiftAssignment> Create(Guid employeeId, BranchId branchId, DateOnly date, ShiftKey shift, DateOnly today, bool busy)
    {
        if (date < today) return ShiftErrors.PastDate;
        if (busy) return ShiftErrors.Busy;
        return new ShiftAssignment { Id = ShiftAssignmentId.New(), EmployeeId = employeeId, BranchId = branchId, Date = date, Shift = shift };
    }

    /// <summary>XC-01 — bỏ ca: không bỏ ca ngày đã qua</summary>
    public Result CanRemove(DateOnly today) => Date < today ? ShiftErrors.PastDate : Result.Success();

    /// <summary>XC-05 — đổi người (đơn đổi ca đã duyệt)</summary>
    public void SwapTo(Guid employeeId) => EmployeeId = employeeId;
}
