using JPCenter.Modules.Finance.Domain.Payroll.Events;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// Bảng lương tháng đã chốt (snapshot) theo (loại, tháng, chi nhánh). Đang khóa → không sửa điều chỉnh / xác nhận buổi tháng đó (TC-13.E2).
/// Vòng đời: chốt → (mở khóa → chốt lại ghi đè cùng dòng, TC-13.E4)* → đã chi (không mở khóa được nữa, TC-13.E3).
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
    /// <summary>Lúc mở khóa gần nhất — có giá trị = đang mở (chưa chốt lại)</summary>
    public DateTimeOffset? UnlockedAt { get; private set; }
    public Guid? UnlockedBy { get; private set; }

    public bool IsLocked => UnlockedAt is null;
    public bool IsPaid => PaidAt is not null;

    public static Result<PayrollLock> Lock(PayrollKind kind, string month, BranchId branchId, Money total, string lines, Guid by, DateTimeOffset at)
    {
        var m = PayrollMonth.Parse(month);
        if (m.IsFailure) return m.Error;
        var l = new PayrollLock { Id = PayrollLockId.New(), Kind = kind, Month = m.Value, BranchId = branchId };
        l.Apply(total, lines, by, at);
        return l;
    }

    /// <summary>Mở khóa để sửa (tầng ứng dụng ghi nhật ký <c>warning</c>)</summary>
    public Result Unlock(Guid by, DateTimeOffset at)
    {
        if (IsPaid) return PayrollErrors.AlreadyPaid;
        if (!IsLocked) return PayrollErrors.NotLocked;
        (UnlockedAt, UnlockedBy) = (at, by);
        return Result.Success();
    }

    /// <summary>Chốt lại sau khi mở khóa — ghi đè snapshot (TC-13.E4)</summary>
    public Result Relock(Money total, string lines, Guid by, DateTimeOffset at)
    {
        if (IsLocked) return PayrollErrors.Locked;
        Apply(total, lines, by, at);
        return Result.Success();
    }

    /// <summary>"Đã chi lương" — chỉ khi đang khóa, 1 lần</summary>
    public Result MarkPaid(DateTimeOffset at)
    {
        if (!IsLocked) return PayrollErrors.NotLocked;
        if (IsPaid) return PayrollErrors.AlreadyPaid;
        PaidAt = at;
        return Result.Success();
    }

    private void Apply(Money total, string lines, Guid by, DateTimeOffset at)
    {
        (Total, Lines, LockedBy, LockedAt, UnlockedAt, UnlockedBy) = (total, lines, by, at, null, null);
        Raise(new PayrollLocked(Id, Kind, Month, BranchId, total));
    }
}
