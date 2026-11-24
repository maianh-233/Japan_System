using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// TC-13 — lương GV = lương cứng + Σ (phút ÷ 60 × đơn giá offline / online) buổi ĐÃ XÁC NHẬN + thưởng dạy thay − phạt đi muộn ± điều chỉnh.
/// Khớp <c>financeUtils.ts › teacherPayroll</c>. Tiền mỗi buổi làm tròn về đồng; thực nhận âm → 0.
/// </summary>
public static class TeacherPayCalculator
{
    public static TeacherPaySlip Calculate(TeacherRates rates, Money baseSalary, IEnumerable<TeachingSession> sessions,
        IEnumerable<decimal> adjustments, PayrollRules rules)
    {
        var ordered = sessions.OrderBy(s => s.Date).ThenBy(s => s.SessionId).ToList();
        var lines = ordered.Where(s => s.Confirmed).Select(s =>
        {
            var rate = rates.For(s.Online);
            var amount = Round(Math.Max(0, s.Minutes) / 60m * rate.Amount);
            var bonus = s.Role == TeachingRole.Substitute ? rules.SubstituteBonus : 0;
            return new TeachingLine(s, rate, Money.FromTrusted(amount), Money.FromTrusted(bonus));
        }).ToList();

        var teaching = lines.Sum(l => l.Amount.Amount);
        var substitute = lines.Sum(l => l.Bonus.Amount);
        var lateFine = lines.Count(l => l.Session.Late) * rules.TeacherLateFine;
        var adjust = adjustments.Sum();
        var total = Math.Max(0, baseSalary.Amount + teaching + substitute - lateFine + adjust);
        var hours = lines.Sum(l => Math.Max(0, l.Session.Minutes)) / 60m;
        return new TeacherPaySlip(lines, ordered.Where(s => !s.Confirmed).ToList(), hours, baseSalary, Money.FromTrusted(teaching),
            Money.FromTrusted(substitute), Money.FromTrusted(lateFine), adjust, Money.FromTrusted(total));
    }

    private static decimal Round(decimal v) => decimal.Round(v, 0, MidpointRounding.AwayFromZero);
}
