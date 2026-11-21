using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Tests.Domain;

public sealed class PricingTests
{
    private static readonly BranchId Cg = new(Guid.NewGuid()), Q1 = new(Guid.NewGuid());
    private static readonly Guid N5 = Guid.NewGuid(), N4 = Guid.NewGuid(), Kaiwa = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 21);

    private static Money M(decimal v) => Money.FromTrusted(v);

    private static DiscountDetails Details(DiscountKind kind = DiscountKind.Percent, decimal value = 10, string code = "khai12",
        IReadOnlyList<Guid>? courses = null, IReadOnlyList<BranchId>? branches = null, int? limit = null, DateOnly? to = null) =>
        new(code, "Khai giảng tháng 12", kind, value, courses ?? [], branches ?? [], Today.AddDays(-5), to ?? Today.AddDays(30), limit, null);

    [Fact]
    public void TC_01_gia_chi_nhanh_phai_lon_hon_0()
    {
        BranchPrice.Create(N5, Cg, Money.Zero).Error.ShouldBe(PricingErrors.InvalidFee);
        var price = BranchPrice.Create(N5, Cg, M(3_200_000)).Value;
        price.ChangeFee(Money.Zero).Error.ShouldBe(PricingErrors.InvalidFee);
        price.ChangeFee(M(3_500_000)).IsSuccess.ShouldBeTrue();
        price.Fee.ShouldBe(M(3_500_000));
    }

    [Fact]
    public void TC_03_tao_uu_dai_va_cac_loi()
    {
        Discount.Create(Details(code: "ab")).Error.ShouldBe(PricingErrors.InvalidCode);
        Discount.Create(Details(code: "KM-12")).Error.ShouldBe(PricingErrors.InvalidCode);
        Discount.Create(Details(value: 51)).Error.ShouldBe(PricingErrors.InvalidPercent);
        Discount.Create(Details(value: 0)).Error.ShouldBe(PricingErrors.InvalidPercent);
        Discount.Create(Details(DiscountKind.Amount, 100_000.5m)).Error.ShouldBe(PricingErrors.InvalidValue);
        Discount.Create(Details(DiscountKind.Combo, 500_000, courses: [N5])).Error.ShouldBe(PricingErrors.ComboCourses);
        Discount.Create(Details(to: Today.AddDays(-10))).Error.ShouldBe(PricingErrors.InvalidDates);
        Discount.Create(Details(limit: 0)).Error.ShouldBe(PricingErrors.InvalidLimit);
        var d = Discount.Create(Details()).Value;
        (d.Code, d.Active, d.Used).ShouldBe(("KHAI12", true, 0));
    }

    [Fact]
    public void TC_03_muc_giam_lam_tron_1000_va_khong_vuot_hoc_phi()
    {
        Discount.Create(Details(value: 7)).Value.Apply(M(3_150_000)).ShouldBe(M(221_000));
        Discount.Create(Details(DiscountKind.Amount, 500_000)).Value.Apply(M(300_000)).ShouldBe(M(300_000));
    }

    [Fact]
    public void TC_03_E5_ap_dung_theo_thoi_gian_chi_nhanh_khoa_combo()
    {
        var d = Discount.Create(Details(branches: [Cg], courses: [N5])).Value;
        d.CheckApplicable(N5, Cg, Today, []).IsSuccess.ShouldBeTrue();
        d.CheckApplicable(N5, Q1, Today, []).Error.ShouldBe(PricingErrors.NotAvailable);
        d.CheckApplicable(N4, Cg, Today, []).Error.ShouldBe(PricingErrors.NotAvailable);
        d.CheckApplicable(N5, Cg, Today.AddDays(31), []).Error.ShouldBe(PricingErrors.NotAvailable);
        d.SetActive(false);
        d.CheckApplicable(N5, Cg, Today, []).Error.ShouldBe(PricingErrors.NotAvailable);

        var combo = Discount.Create(Details(DiscountKind.Combo, 800_000, courses: [N5, Kaiwa])).Value;
        combo.CheckApplicable(N5, Cg, Today, []).Error.ShouldBe(PricingErrors.NotAvailable);
        combo.CheckApplicable(N5, Cg, Today, [Kaiwa]).IsSuccess.ShouldBeTrue();
        combo.CheckApplicable(N4, Cg, Today, [N5, Kaiwa]).Error.ShouldBe(PricingErrors.NotAvailable);
    }

    [Fact]
    public void TC_03_E4_E7_het_luot_va_tra_luot()
    {
        var d = Discount.Create(Details(limit: 1)).Value;
        d.Use().IsSuccess.ShouldBeTrue();
        d.Use().Error.ShouldBe(PricingErrors.LimitReached);
        d.CheckApplicable(N5, Cg, Today, []).Error.ShouldBe(PricingErrors.LimitReached);
        d.Update(Details(limit: 0)).Error.ShouldBe(PricingErrors.InvalidLimit);
        d.Release();
        d.Used.ShouldBe(0);
        d.Use().IsSuccess.ShouldBeTrue();
        d.Use();
        d.Update(Details(limit: 2)).IsSuccess.ShouldBeTrue();
        d.Use().IsSuccess.ShouldBeTrue();
        d.Update(Details(limit: 1)).Error.ShouldBe(PricingErrors.LimitBelowUsed);
    }

    [Fact]
    public void TC_02_chinh_sach_dot_tong_100_va_lich_dong()
    {
        InstallmentPlan.Create("Hai đợt", [new(100, 0)], Money.Zero, 0).Error.ShouldBe(PricingErrors.PlanParts);
        InstallmentPlan.Create("Hai đợt", [new(50, -7), new(0, 30)], Money.Zero, 0).Error.ShouldBe(PricingErrors.PlanParts);
        var total = InstallmentPlan.Create("Hai đợt", [new(50, -7), new(40, 30)], Money.Zero, 0).Error;
        (total.Code, total.Message).ShouldBe(("finance.installment.total", "Tổng các đợt phải bằng 100% (hiện 90%)"));
        InstallmentPlan.Create("Hai đợt", [new(50, -7), new(50, 30)], Money.Zero, 60).Error.ShouldBe(PricingErrors.InvalidSurcharge);

        var plan = InstallmentPlan.Create("Ba đợt", [new(40, -7), new(30, 30), new(30, 60)], M(5_000_000), 3).Value;
        plan.AppliesTo(M(4_000_000)).ShouldBeFalse();
        plan.AppliesTo(M(6_000_000)).ShouldBeTrue();
        plan.PriceWithSurcharge(M(6_250_000)).ShouldBe(M(6_438_000));

        var start = new DateOnly(2026, 12, 7);
        var schedule = plan.ScheduleFor(M(6_438_000), start);
        schedule.Select(s => s.Amount.Amount).ShouldBe([2_575_000m, 1_931_000m, 1_932_000m]);
        schedule.Select(s => s.DueDate).ShouldBe([start.AddDays(-7), start.AddDays(30), start.AddDays(60)]);
        plan.SetActive(false);
        plan.AppliesTo(M(6_000_000)).ShouldBeFalse();
    }
}
