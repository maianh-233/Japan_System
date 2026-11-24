using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Tests.Domain;

public sealed class CommissionTests
{
    private static readonly BranchId Branch = new(Guid.NewGuid());
    private static readonly Guid Staff = Guid.NewGuid();

    private static Money M(decimal v) => Money.FromTrusted(v);

    private static List<CommissionTier> Tiers() =>
        CommissionTable.Build([(M(0), 2), (M(50_000_000), 4), (M(20_000_000), 3)]).Value;

    [Fact]
    public void TC_12_bang_bac_xep_tang_dan_va_chon_bac_theo_thuc_thu()
    {
        var tiers = Tiers();
        tiers.Select(t => t.From).ShouldBe([M(0), M(20_000_000), M(50_000_000)]);
        CommissionTable.TierOf(tiers, 19_999_999)!.Percent.ShouldBe(2);
        CommissionTable.TierOf(tiers, 20_000_000)!.Percent.ShouldBe(3);
        CommissionTable.CommissionOf(tiers, 30_000_000).ShouldBe(M(900_000)); // cả số × 3%, không lũy tiến
        CommissionTable.CommissionOf(tiers, -1_000_000).ShouldBe(Money.Zero);
    }

    [Fact]
    public void TC_12_E1_E2_bac_khong_hop_le()
    {
        CommissionTable.Build([(M(1_000), 2)]).Error.ShouldBe(CommissionErrors.ZeroTierRequired);
        CommissionTable.Build([]).Error.ShouldBe(CommissionErrors.ZeroTierRequired);
        CommissionTable.Build([(M(0), 2), (M(0), 3)]).Error.ShouldBe(CommissionErrors.DuplicateTier);
        CommissionTable.Build([(M(0), 101)]).Error.ShouldBe(CommissionErrors.InvalidPercent);
        CommissionTable.Build([(M(0), -1)]).Error.ShouldBe(CommissionErrors.InvalidPercent);
    }

    [Fact]
    public void TC_12_ket_qua_thang_KPI_va_hoa_hong()
    {
        var r = SalePerformance.Of(Staff, 3, M(40_000_000), 25_000_000, M(50_000_000), Tiers());
        r.Kpi.ShouldBe(0.5m);
        r.Tier!.Percent.ShouldBe(3);
        r.Commission.ShouldBe(M(750_000));
        SalePerformance.Of(Staff, 0, Money.Zero, -2_000_000, Money.Zero, Tiers()).ShouldSatisfyAllConditions(
            x => x.Kpi.ShouldBe(0), x => x.Tier.ShouldBeNull(), x => x.Commission.ShouldBe(Money.Zero));
    }

    [Fact]
    public void Chi_tieu_thang()
    {
        SaleTarget.Create(Staff, "2026-11", -1).Error.ShouldBe(CommissionErrors.InvalidTarget);
        SaleTarget.Create(Staff, "2026/11", 1).Error.ShouldBe(PayrollErrors.InvalidMonth);
        var t = SaleTarget.Create(Staff, "2026-11", 80_000_000).Value;
        t.Change(0.5m).Error.ShouldBe(CommissionErrors.InvalidTarget);
        t.Change(90_000_000).IsSuccess.ShouldBeTrue();
        t.Target.ShouldBe(M(90_000_000));
    }

    [Fact]
    public void TC_04_E5_chon_Sale_ghi_nhan()
    {
        Guid student = Guid.NewGuid(), won = Guid.NewGuid(), latest = Guid.NewGuid(), s1 = Guid.NewGuid(), s2 = Guid.NewGuid();
        SaleAttribution.Pick(student, won, latest, [s1]).ShouldBe(won);
        SaleAttribution.Pick(student, null, latest, [s1]).ShouldBe(latest);
        SaleAttribution.Pick(student, null, null, []).ShouldBeNull();
        var a = SaleAttribution.Pick(student, null, null, [s1, s2]);
        a.ShouldNotBeNull();
        SaleAttribution.Pick(student, null, null, [s2, s1, s2]).ShouldBe(a); // cố định theo HV, không phụ thuộc thứ tự
    }

    [Fact]
    public void TC_12_gan_lai_Sale_ghi_de_tu_chon()
    {
        var inv = Invoice.Issue("HD-CG-2611-0001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Branch, M(3_000_000), Money.Zero, null,
            new DateOnly(2026, 11, 30), "HD CG2611 0001", DateTimeOffset.UtcNow).Value;
        Guid auto = Guid.NewGuid(), manual = Guid.NewGuid();
        inv.AttributeSale(auto);
        inv.SaleId.ShouldBe(auto);
        inv.ReassignSale(Guid.Empty, Staff).Error.ShouldBe(CommissionErrors.SaleRequired);
        inv.ReassignSale(manual, Staff).IsSuccess.ShouldBeTrue();
        inv.AttributeSale(auto);
        inv.SaleId.ShouldBe(manual);
        inv.SaleAssignedBy.ShouldBe(Staff);
    }

    [Fact]
    public void TC_12_E3_thuc_thu_theo_thang_hoan_thang_sau_tru_vao_thang_hoan()
    {
        var nov = new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero); // 00:00 01/11 giờ VN
        var dec = nov.AddMonths(1);
        var inv = Invoice.Issue("HD-CG-2611-0002", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Branch, M(5_000_000), Money.Zero, null,
            new DateOnly(2026, 11, 30), "HD CG2611 0002", nov).Value;
        inv.Pay(M(5_000_000), PaymentMethod.Cash, nov.AddDays(2), Staff).IsSuccess.ShouldBeTrue();
        inv.RequestRefund(M(1_000_000), PaymentMethod.Cash, "Chuyển lớp", Staff, needsApproval: false, dec.AddDays(3)).IsSuccess.ShouldBeTrue();

        inv.NetCollectedBetween(nov, dec).ShouldBe(5_000_000);
        inv.NetCollectedBetween(dec, dec.AddMonths(1)).ShouldBe(-1_000_000);
    }
}
