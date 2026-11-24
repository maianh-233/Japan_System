using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Thuế TNCN lũy tiến theo tháng (7 bậc) — khớp <c>financeUtils.ts › pitOf</c></summary>
public static class PersonalIncomeTax
{
    private static readonly (decimal Cap, decimal Rate)[] Brackets =
    [
        (5_000_000, 0.05m), (10_000_000, 0.10m), (18_000_000, 0.15m), (32_000_000, 0.20m),
        (52_000_000, 0.25m), (80_000_000, 0.30m), (decimal.MaxValue, 0.35m),
    ];

    /// <summary>Thuế của thu nhập tính thuế (đã trừ phạt, BHXH, giảm trừ); ≤ 0 → 0</summary>
    public static Money Of(decimal taxable)
    {
        if (taxable <= 0) return Money.Zero;
        decimal tax = 0, prev = 0;
        foreach (var (cap, rate) in Brackets)
        {
            if (taxable <= prev) break;
            tax += (Math.Min(taxable, cap) - prev) * rate;
            prev = cap;
        }
        return Money.FromTrusted(decimal.Round(tax, 0, MidpointRounding.AwayFromZero));
    }
}
