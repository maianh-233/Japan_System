using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Ca làm được xếp cho nhân viên "làm theo ca". Bảng: work.shift_assignments, UQ (employee_id, date, shift)</summary>
public sealed class ShiftAssignment : AggregateRoot<ShiftAssignmentId>, IBranchScoped, IAuditable
{
    private ShiftAssignment() { }

    /// <summary>Hr.Employee</summary>
    public Guid EmployeeId { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateOnly Date { get; private set; }
    public ShiftKey Shift { get; private set; }

    public static ShiftAssignment Create(Guid employeeId, BranchId branchId, DateOnly date, ShiftKey shift) =>
        new() { Id = ShiftAssignmentId.New(), EmployeeId = employeeId, BranchId = branchId, Date = date, Shift = shift };

    /// <summary>Đổi ca (đơn đổi ca đã duyệt)</summary>
    public void SwapTo(Guid employeeId) => EmployeeId = employeeId;
}
