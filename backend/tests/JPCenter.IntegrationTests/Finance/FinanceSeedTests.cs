using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Finance;

/// <summary>Giai đoạn 19 phase 2 — dữ liệu mẫu Tài chính nạp đúng từ mock frontend (user story 08).</summary>
[Collection(SeededCollection.Name)]
public sealed class FinanceSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonElement Seed = SeedData.Read<JsonElement>(typeof(FinanceDbContext).Assembly, "finance.seed.json");

    private static List<JsonElement> Items(string name) => Seed.GetProperty(name).EnumerateArray().ToList();

    private static Guid Id(JsonElement e) => SeedData.IdOf(e.GetProperty("id").GetString()!);

    private static decimal Sum(JsonElement e, string list) => e.GetProperty(list).EnumerateArray().Sum(x => x.GetProperty("amount").GetDecimal());

    [Fact]
    public async Task Hoa_don_thu_hoan_huy_dung_theo_mock()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var mock = Items("invoices");
        var invoices = await db.Invoices.AsNoTracking().ToListAsync(Ct);

        invoices.Count.ShouldBe(mock.Count);
        foreach (var m in mock)
        {
            var inv = invoices.Single(i => i.Id == new InvoiceId(Id(m)));
            inv.Code.ShouldBe(m.GetProperty("code").GetString());
            inv.Total.Amount.ShouldBe(m.GetProperty("amount").GetDecimal() - m.GetProperty("discount").GetDecimal(), inv.Code);
            inv.Collected.Amount.ShouldBe(Sum(m, "payments"), inv.Code);
            inv.RefundedAmount.Amount.ShouldBe(Sum(m, "refunds"), inv.Code);
            inv.Status.ShouldBe(m.GetProperty("cancelled").GetBoolean() ? InvoiceStatus.Cancelled : InvoiceStatus.Open, inv.Code);
            inv.TransferContent.ShouldStartWith(inv.Code);
        }

        // Khoản thu giữ Id + người thu + giao dịch ngân hàng theo mock
        var paid = invoices.SelectMany(i => i.Payments).ToList();
        paid.ShouldContain(p => p.Id == new PaymentId(SeedData.IdOf("P-0-1")) && p.CollectorId == SeedData.IdOf("e-lan2"));
        paid.Count(p => p.BankTransactionId != null).ShouldBe(mock.Sum(m => m.GetProperty("payments").EnumerateArray()
            .Count(p => p.GetProperty("bankTransactionId").ValueKind == JsonValueKind.String)));

        // Ngày tạo theo ngày lập hóa đơn (không phải lúc nạp)
        var first = mock[0];
        var created = await db.Invoices.Where(i => i.Id == new InvoiceId(Id(first)))
            .Select(i => EF.Property<DateTimeOffset>(i, "CreatedAt")).SingleAsync(Ct);
        created.ShouldBe(first.GetProperty("issuedAt").GetDateTimeOffset(), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Bo_dem_so_hoa_don_bang_so_lon_nhat_da_dung()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var counters = await db.InvoiceCounters.AsNoTracking().ToListAsync(Ct);
        var codes = await db.Invoices.AsNoTracking().Select(i => new { i.BranchId, i.Code }).ToListAsync(Ct);

        counters.ShouldNotBeEmpty();
        foreach (var c in counters)
        {
            var yymm = c.Period[2..4] + c.Period[5..7];
            var max = codes.Where(x => x.BranchId == c.BranchId && x.Code.Split('-')[2] == yymm).Max(x => int.Parse(x.Code.Split('-')[3]));
            c.Last.ShouldBe(max, $"{c.BranchId} {c.Period}");
        }
    }

    [Fact]
    public async Task Bang_gia_uu_dai_dong_dot_giao_dich_nhac_hoc_phi()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

        (await db.BranchPrices.CountAsync(Ct)).ShouldBe(Items("branchPrices").Count);

        var discounts = await db.Discounts.AsNoTracking().ToListAsync(Ct);
        discounts.Count.ShouldBe(Items("discounts").Count);
        var sv = discounts.Single(d => d.Code == "SV15");
        (sv.Limit, sv.Used).ShouldBe((40, 12));
        sv.BranchIds.ShouldBe([new BranchId(SeedData.IdOf("cg"))]);

        // "Đóng 1 lần" (1 đợt) không phải chính sách đóng đợt hợp lệ → bỏ qua
        var plans = await db.InstallmentPlans.AsNoTracking().ToListAsync(Ct);
        plans.Count.ShouldBe(Items("installmentPlans").Count - 1);
        plans.ShouldAllBe(p => p.Parts.Count >= 2 && p.Parts.Sum(x => x.Percent) == 100);

        var txns = await db.BankTransactions.AsNoTracking().ToListAsync(Ct);
        txns.Count.ShouldBe(Items("bankTransactions").Count);
        txns.Where(t => t.Status == TransactionStatus.Auto).ShouldAllBe(t => t.InvoiceIds.Count > 0 && t.MatchedBy == null);
        txns.Single(t => t.Status == TransactionStatus.Ignored).Note.ShouldNotBeNullOrWhiteSpace();

        var reminders = await db.PaymentReminders.AsNoTracking().ToListAsync(Ct);
        reminders.Count.ShouldBe(Items("reminders").Count);
        reminders.Where(r => r.By == null).ShouldAllBe(r => r.Milestone != null);
        reminders.Where(r => r.By != null).ShouldAllBe(r => r.Milestone == null);
    }
}
