using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// Bảng lương tháng đã chốt (snapshot) — sau khi chốt không sửa buổi dạy / chấm công của tháng đó.
/// Bảng: finance.payroll_locks, UQ (kind, month, branch_id)
/// </summary>
public sealed class PayrollLock : AggregateRoot<PayrollLockId>, IBranchScoped
{
    private PayrollLock() { }

    public PayrollKind Kind { get; private set; }
    public string Month { get; private set; } = null!;
    public BranchId BranchId { get; private set; }
    public Guid LockedBy { get; private set; }
    public DateTimeOffset LockedAt { get; private set; }
    public Money Total { get; private set; }
    /// <summary>Chi tiết từng người lúc chốt (jsonb: employeeId, các khoản, thực nhận)</summary>
    public string Lines { get; private set; } = null!;
    /// <summary>Đã chuyển khoản lương</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    public static PayrollLock Lock(PayrollKind kind, string month, BranchId branchId, Money total, string lines, Guid by, DateTimeOffset at) =>
        new() { Id = PayrollLockId.New(), Kind = kind, Month = month, BranchId = branchId, Total = total, Lines = lines, LockedBy = by, LockedAt = at };

    public void MarkPaid(DateTimeOffset at) => PaidAt = at;
}
