using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Chỉ tiêu doanh số tháng của 1 Sale. Bảng: finance.sale_targets, UQ (employee_id, month)</summary>
public sealed class SaleTarget : AggregateRoot<SaleTargetId>, IAuditable
{
    private SaleTarget() { }

    /// <summary>Hr.Employee</summary>
    public Guid EmployeeId { get; private set; }
    /// <summary>YYYY-MM</summary>
    public string Month { get; private set; } = null!;
    public Money Target { get; private set; }

    public static SaleTarget Create(Guid employeeId, string month, Money target) =>
        new() { Id = SaleTargetId.New(), EmployeeId = employeeId, Month = month, Target = target };

    public void Change(Money target) => Target = target;
}
