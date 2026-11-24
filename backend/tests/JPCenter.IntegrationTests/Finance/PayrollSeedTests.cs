using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Finance;

/// <summary>Giai đoạn 22 phase 2 — dữ liệu mẫu lương / hoa hồng nạp đúng từ mock; khóa bí mật tích hợp được mã hóa (story 08, 15).</summary>
[Collection(SeededCollection.Name)]
public sealed class PayrollSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonElement Seed = SeedData.Read<JsonElement>(typeof(FinanceDbContext).Assembly, "payroll.seed.json");

    private static List<JsonElement> Items(string name) => Seed.GetProperty(name).EnumerateArray().ToList();

    [Fact]
    public async Task Dieu_chinh_bang_luong_bac_chi_tieu_dung_theo_mock()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

        var adjust = Items("payAdjustments")[0];
        var a = await db.PayAdjustments.AsNoTracking().SingleAsync(x => x.Id == new PayAdjustmentId(SeedData.IdOf(adjust.GetProperty("id").GetString()!)), Ct);
        a.Amount.ShouldBe(adjust.GetProperty("amount").GetDecimal());
        a.EmployeeId.ShouldBe(SeedData.IdOf(adjust.GetProperty("employeeId").GetString()!));
        a.Month.ShouldBe(adjust.GetProperty("month").GetString());

        var locks = await db.PayrollLocks.AsNoTracking().IgnoreQueryFilters().ToListAsync(Ct);
        locks.Count.ShouldBeGreaterThanOrEqualTo(Items("payrollLocks").Count);
        locks.ShouldContain(l => l.Kind == PayrollKind.Teacher && l.Month == Items("payrollLocks")[0].GetProperty("month").GetString()
            && l.IsLocked && l.IsPaid && l.Lines == "[]");

        var tiers = await db.CommissionTiers.AsNoTracking().OrderBy(t => t.From).ToListAsync(Ct);
        tiers.Select(t => t.From.Amount).ShouldBe(Items("commissionTiers").Select(t => t.GetProperty("from").GetDecimal()));
        tiers[0].From.ShouldBe(Money.Zero);

        (await db.SaleTargets.CountAsync(Ct)).ShouldBe(Items("saleTargets").Count);
    }

    [Fact]
    public async Task Hoa_don_mau_co_Sale_ghi_nhan_theo_quy_tac_frontend()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var expected = Items("invoiceSales").ToDictionary(
            x => new InvoiceId(SeedData.IdOf(x.GetProperty("invoiceId").GetString()!)), x => SeedData.IdOf(x.GetProperty("saleId").GetString()!));

        var invoices = (await db.Invoices.AsNoTracking().ToListAsync(Ct)).Where(i => expected.ContainsKey(i.Id)).ToList();
        invoices.Count.ShouldBe(expected.Count);
        invoices.ShouldAllBe(i => i.SaleId == expected[i.Id] && i.SaleAssignedBy == null);
    }

    [Fact]
    public async Task Khoa_bi_mat_tich_hop_duoc_ma_hoa_va_giai_ma_lai_duoc()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        var secrets = scope.ServiceProvider.GetRequiredService<IIntegrationSecrets>();

        var zalo = await db.Integrations.AsNoTracking().SingleAsync(i => i.Key == IntegrationKeys.ZaloOa, Ct);
        zalo.Config["secret"].ShouldStartWith("enc:");
        zalo.Config["oaId"].ShouldNotStartWith("enc:");
        secrets.Reveal(zalo.Config["secret"]).ShouldBe("zx9K2mPq7vL0aB4n");
        zalo.MaskedConfig(secrets.Reveal)["secret"].ShouldBe("••••••aB4n");
        secrets.Protect(zalo.Config["secret"]).ShouldBe(zalo.Config["secret"]); // không mã hóa chồng
    }
}
