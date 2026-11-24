using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// Thưởng (+) / phạt (−) / phụ cấp thêm cho 1 người trong 1 tháng (TC-13 bước 3). Tháng đang khóa thì tầng ứng dụng chặn thêm / xóa
/// (<see cref="PayrollErrors.Locked"/>). Bảng: finance.pay_adjustments
/// </summary>
public sealed class PayAdjustment : AggregateRoot<PayAdjustmentId>, IAuditable, ISoftDeletable
{
    public const int MaxReason = 500;

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
        var m = PayrollMonth.Parse(month);
        if (m.IsFailure) return m.Error;
        if (amount == 0 || decimal.Truncate(amount) != amount) return PayrollErrors.InvalidAdjustAmount;
        var r = reason?.Trim();
        if (string.IsNullOrEmpty(r)) return PayrollErrors.AdjustReasonRequired;
        if (r.Length > MaxReason) r = r[..MaxReason];
        return new PayAdjustment { Id = PayAdjustmentId.New(), Month = m.Value, EmployeeId = employeeId, Amount = amount, Reason = r, By = by };
    }
}
