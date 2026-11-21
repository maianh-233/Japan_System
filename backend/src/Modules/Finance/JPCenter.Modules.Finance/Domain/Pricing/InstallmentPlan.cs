using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>
/// Chính sách đóng nhiều đợt (TC-02): % mỗi đợt (tổng 100), hạn = ngày khai giảng + offsetDays (âm = trước khai giảng),
/// áp cho học phí từ <see cref="MinFee"/>, phụ thu %. Bảng: finance.installment_plans
/// </summary>
public sealed class InstallmentPlan : AggregateRoot<InstallmentPlanId>, IAuditable
{
    public const decimal MaxSurcharge = 50;
    /// <summary>Tiền mỗi đợt / phụ thu làm tròn tới 1.000đ (đợt cuối nhận phần lẻ)</summary>
    public const decimal RoundTo = 1000;

    private InstallmentPlan() { }

    public string Name { get; private set; } = null!;
    /// <summary>Các đợt (jsonb) — tổng % = 100</summary>
    public List<InstallmentPart> Parts { get; private set; } = [];
    /// <summary>Áp dụng cho khóa có học phí từ</summary>
    public Money MinFee { get; private set; }
    /// <summary>Phụ thu khi đóng nhiều đợt (%)</summary>
    public decimal Surcharge { get; private set; }
    public bool Active { get; private set; }

    public static Result<InstallmentPlan> Create(string? name, IReadOnlyList<InstallmentPart> parts, Money minFee, decimal surcharge)
    {
        var plan = new InstallmentPlan { Id = InstallmentPlanId.New(), Active = true };
        var set = plan.Update(name, parts, minFee, surcharge);
        return set.IsFailure ? set.Error : plan;
    }

    public Result Update(string? name, IReadOnlyList<InstallmentPart> parts, Money minFee, decimal surcharge)
    {
        if (string.IsNullOrWhiteSpace(name)) return PricingErrors.PlanNameRequired;
        if (parts.Count < 2 || parts.Any(p => p.Percent <= 0)) return PricingErrors.PlanParts;
        var total = parts.Sum(p => p.Percent);
        if (total != 100) return PricingErrors.PlanTotal(total);
        if (surcharge is < 0 or > MaxSurcharge) return PricingErrors.InvalidSurcharge;
        (Name, Parts, MinFee, Surcharge) = (name.Trim(), parts.OrderBy(p => p.OffsetDays).ToList(), minFee, surcharge);
        return Result.Success();
    }

    public void SetActive(bool active) => Active = active;

    /// <summary>Dùng được cho học phí <paramref name="fee"/>: đang bật và học phí ≥ mức áp dụng</summary>
    public bool AppliesTo(Money fee) => Active && fee >= MinFee;

    /// <summary>Học phí khi đóng nhiều đợt = học phí + phụ thu (làm tròn 1.000đ)</summary>
    public Money PriceWithSurcharge(Money fee) =>
        fee + Money.FromTrusted(decimal.Round(fee.Amount * Surcharge / 100m / RoundTo, 0, MidpointRounding.AwayFromZero) * RoundTo);

    /// <summary>
    /// Lịch đóng cho số phải thu <paramref name="total"/>: mỗi đợt = % × phải thu (làm tròn 1.000đ), đợt cuối nhận phần còn lại;
    /// hạn = <paramref name="classStart"/> + offset. Đợt có số tiền 0 (phải thu quá nhỏ) bị bỏ.
    /// </summary>
    public IReadOnlyList<InstallmentDue> ScheduleFor(Money total, DateOnly classStart)
    {
        var result = new List<InstallmentDue>();
        var assigned = 0m;
        for (var i = 0; i < Parts.Count; i++)
        {
            var part = Parts[i];
            var amount = i == Parts.Count - 1
                ? total.Amount - assigned
                : Math.Min(total.Amount - assigned, decimal.Round(total.Amount * part.Percent / 100m / RoundTo, 0, MidpointRounding.AwayFromZero) * RoundTo);
            assigned += amount;
            if (amount > 0) result.Add(new InstallmentDue(Money.FromTrusted(amount), classStart.AddDays(part.OffsetDays)));
        }
        return result;
    }
}
