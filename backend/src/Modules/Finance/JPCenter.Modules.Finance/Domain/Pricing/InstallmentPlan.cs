using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Chính sách đóng nhiều đợt: % mỗi đợt, hạn = ngày khai giảng + offsetDays, phụ thu. Bảng: finance.installment_plans</summary>
public sealed class InstallmentPlan : AggregateRoot<InstallmentPlanId>, IAuditable
{
    private InstallmentPlan() { }

    public string Name { get; private set; } = null!;
    /// <summary>Các đợt (jsonb) — tổng % = 100</summary>
    public List<InstallmentPart> Parts { get; private set; } = [];
    /// <summary>Áp dụng cho khóa có học phí từ</summary>
    public Money MinFee { get; private set; }
    /// <summary>Phụ thu khi đóng nhiều đợt (%)</summary>
    public decimal Surcharge { get; private set; }
    public bool Active { get; private set; }

    public static Result<InstallmentPlan> Create(string name, List<InstallmentPart> parts, Money minFee, decimal surcharge)
    {
        if (parts.Count < 2) return Error.Validation("finance.installment.parts", "Cần ít nhất 2 đợt");
        if (parts.Sum(p => p.Percent) != 100) return Error.Validation("finance.installment.total", "Tổng % các đợt phải bằng 100");
        return new InstallmentPlan { Id = InstallmentPlanId.New(), Name = name, Parts = parts, MinFee = minFee, Surcharge = surcharge, Active = true };
    }

    public void SetActive(bool active) => Active = active;
}
