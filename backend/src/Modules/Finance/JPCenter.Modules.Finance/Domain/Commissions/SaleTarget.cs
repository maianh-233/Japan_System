using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Chỉ tiêu doanh số tháng của 1 Sale (TC-12 bước 2). Bảng: finance.sale_targets, UQ (employee_id, month)</summary>
public sealed class SaleTarget : AggregateRoot<SaleTargetId>, IAuditable
{
    private SaleTarget() { }

    /// <summary>Hr.Employee</summary>
    public Guid EmployeeId { get; private set; }
    /// <summary>YYYY-MM</summary>
    public string Month { get; private set; } = null!;
    public Money Target { get; private set; }

    public static Result<SaleTarget> Create(Guid employeeId, string month, decimal target)
    {
        var m = PayrollMonth.Parse(month);
        if (m.IsFailure) return m.Error;
        var t = Money.Create(target);
        if (t.IsFailure) return CommissionErrors.InvalidTarget;
        return new SaleTarget { Id = SaleTargetId.New(), EmployeeId = employeeId, Month = m.Value, Target = t.Value };
    }

    public Result Change(decimal target)
    {
        var t = Money.Create(target);
        if (t.IsFailure) return CommissionErrors.InvalidTarget;
        Target = t.Value;
        return Result.Success();
    }
}
