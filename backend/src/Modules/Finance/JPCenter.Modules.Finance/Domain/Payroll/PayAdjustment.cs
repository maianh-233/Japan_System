using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Thưởng (+) / phạt (−) / phụ cấp thêm cho 1 người trong 1 tháng. Bảng: finance.pay_adjustments</summary>
public sealed class PayAdjustment : AggregateRoot<PayAdjustmentId>, IAuditable, ISoftDeletable
{
    private PayAdjustment() { }

    /// <summary>Tháng lương YYYY-MM</summary>
    public string Month { get; private set; } = null!;
    /// <summary>Hr.Employee</summary>
    public Guid EmployeeId { get; private set; }
    /// <summary>Số tiền: dương = thưởng / phụ cấp, âm = phạt / khấu trừ (numeric, có dấu)</summary>
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid By { get; private set; }

    public static Result<PayAdjustment> Create(string month, Guid employeeId, decimal amount, string reason, Guid by)
    {
        if (amount == 0 || decimal.Truncate(amount) != amount) return Error.Validation("finance.pay_adjust.invalid_amount", "Số tiền phải khác 0 và không có số lẻ");
        if (string.IsNullOrWhiteSpace(reason)) return Error.Validation("finance.pay_adjust.reason_required", "Cần ghi lý do");
        return new PayAdjustment { Id = PayAdjustmentId.New(), Month = month, EmployeeId = employeeId, Amount = amount, Reason = reason, By = by };
    }
}
