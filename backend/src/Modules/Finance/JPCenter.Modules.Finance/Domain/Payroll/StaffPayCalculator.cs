using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// TC-14 — lương NV = lương cứng × công ÷ công chuẩn + phụ cấp + hoa hồng (TC-12) ± điều chỉnh − phạt − BHXH (theo lương cứng) − thuế TNCN.
/// Khớp <c>financeUtils.ts › staffPayroll</c>.
/// </summary>
public static class StaffPayCalculator
{
    public static StaffPaySlip Calculate(StaffContract contract, StaffWorkDays days, Money commission, IEnumerable<decimal> adjustments,
        PayrollRules rules)
    {
        var payDays = days.HasTimesheet ? Math.Min(rules.StandardDays, days.Worked + days.PaidLeave) : rules.StandardDays;
        var basePay = Round(contract.BaseSalary.Amount * payDays / rules.StandardDays);
        var adjust = adjustments.Sum();
        var gross = basePay + contract.Allowance.Amount + commission.Amount + adjust;
        var fines = days.Late * rules.LateFine + days.NoCheckOut * rules.NoCheckOutFine;
        var insurance = contract.Probation ? 0 : Round(contract.BaseSalary.Amount * rules.InsuranceRate);
        var tax = PersonalIncomeTax.Of(gross - fines - insurance - rules.PersonalDeduction);
        var net = Math.Max(0, gross - fines - insurance - tax.Amount);
        return new StaffPaySlip(days, payDays, contract.BaseSalary, Money.FromTrusted(basePay), contract.Allowance, commission, adjust, gross,
            Money.FromTrusted(fines), Money.FromTrusted(insurance), tax, Money.FromTrusted(net));
    }

    private static decimal Round(decimal v) => decimal.Round(v, 0, MidpointRounding.AwayFromZero);
}
