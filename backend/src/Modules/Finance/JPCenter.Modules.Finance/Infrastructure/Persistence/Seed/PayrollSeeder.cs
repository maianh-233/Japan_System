using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp dữ liệu mẫu lương / hoa hồng (giai đoạn 22) từ <c>payroll.seed.json</c>: điều chỉnh lương, bảng lương đã chốt, bậc hoa hồng,
/// chỉ tiêu Sale — mỗi bảng chỉ nạp khi CÒN TRỐNG. Sale ghi nhận của hóa đơn (TC-04.E5, chọn theo đúng quy tắc frontend) chỉ điền
/// khi chưa hóa đơn nào có Sale — DB đã có hóa đơn từ giai đoạn 19 cũng được bổ sung. Không xóa gì.
/// </summary>
internal sealed class PayrollSeeder(FinanceDbContext db, ILogger<PayrollSeeder> logger) : IDataSeeder
{
    /// <summary>Sau <see cref="FinanceSeeder"/> (cần hóa đơn đã có)</summary>
    public int Order => 51;

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<PayrollSeedFile>(typeof(PayrollSeeder).Assembly, "payroll.seed.json");
        var counts = new List<string>();

        if (!await db.PayAdjustments.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedAdjustments(data)} điều chỉnh lương");
        if (!await db.PayrollLocks.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedLocks(data)} bảng lương đã chốt");
        if (!await db.CommissionTiers.AnyAsync(ct)) counts.Add($"{SeedTiers(data)} bậc hoa hồng");
        if (!await db.SaleTargets.AnyAsync(ct)) counts.Add($"{SeedTargets(data)} chỉ tiêu Sale");
        if (!await db.Invoices.IgnoreQueryFilters().AnyAsync(i => i.SaleId != null, ct))
            counts.Add($"{await SeedInvoiceSales(data, ct)} hóa đơn ghi nhận Sale");

        if (counts.Count == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dữ liệu mẫu lương / hoa hồng: {Counts}", string.Join(", ", counts));
    }

    private int SeedAdjustments(PayrollSeedFile data)
    {
        var added = 0;
        foreach (var a in data.PayAdjustments)
        {
            var created = PayAdjustment.Create(a.Month, SeedData.IdOf(a.EmployeeId), a.Amount, a.Reason, Person(a.ById));
            if (Skip(created, a.Id)) continue;
            db.AddWithId(created.Value, SeedData.IdOf(a.Id));
            added++;
        }
        return added;
    }

    /// <summary>Mock chốt tháng trước với tổng 0đ và chưa có chi tiết từng người → snapshot rỗng <c>[]</c></summary>
    private int SeedLocks(PayrollSeedFile data)
    {
        var added = 0;
        foreach (var l in data.PayrollLocks)
        {
            var created = PayrollLock.Lock(SnakeCaseEnumConverter<PayrollKind>.Parse(l.Kind), l.Month, new BranchId(SeedData.IdOf(l.BranchId)),
                Money.FromTrusted(l.Total), "[]", Person(l.LockedById), l.LockedAt);
            if (Skip(created, $"{l.Kind}/{l.Month}/{l.BranchId}")) continue;
            var payroll = created.Value;
            if (l.PaidAt is { } paid && SkipResult(payroll.MarkPaid(paid), $"{l.Kind}/{l.Month}/{l.BranchId} đã chi")) continue;
            payroll.ClearDomainEvents();
            db.Add(payroll);
            added++;
        }
        return added;
    }

    private int SeedTiers(PayrollSeedFile data)
    {
        var built = CommissionTable.Build(data.CommissionTiers.Select(t => (Money.FromTrusted(t.From), t.Percent)));
        if (Skip(built, "bậc hoa hồng")) return 0;
        db.CommissionTiers.AddRange(built.Value);
        return built.Value.Count;
    }

    private int SeedTargets(PayrollSeedFile data)
    {
        var added = 0;
        foreach (var t in data.SaleTargets)
        {
            var created = SaleTarget.Create(SeedData.IdOf(t.EmployeeId), t.Month, t.Target);
            if (Skip(created, $"{t.EmployeeId}/{t.Month}")) continue;
            db.Add(created.Value);
            added++;
        }
        return added;
    }

    private async Task<int> SeedInvoiceSales(PayrollSeedFile data, CancellationToken ct)
    {
        var saleOf = data.InvoiceSales.ToDictionary(x => new InvoiceId(SeedData.IdOf(x.InvoiceId)), x => SeedData.IdOf(x.SaleId));
        var invoices = (await db.Invoices.IgnoreQueryFilters().ToListAsync(ct)).Where(i => saleOf.ContainsKey(i.Id)).ToList();
        foreach (var invoice in invoices) invoice.AttributeSale(saleOf[invoice.Id]);
        return invoices.Count;
    }

    // ------------------------------------------------------------------ Tiện ích

    private bool Skip<T>(Result<T> result, string label) => SkipResult(result, label);

    private bool SkipResult(Result result, string label)
    {
        if (result.IsSuccess) return false;
        logger.LogWarning("Bỏ qua dữ liệu mẫu lương / hoa hồng {Label}: {Error}", label, result.Error.Message);
        return true;
    }

    private static Guid Person(string? mockId) => mockId is null ? Guid.Empty : SeedData.IdOf(mockId);

    // ------------------------------------------------------------------ Định dạng file

    private sealed record PayrollSeedFile(List<SeedAdjustment> PayAdjustments, List<SeedLock> PayrollLocks, List<SeedTier> CommissionTiers,
        List<SeedTarget> SaleTargets, List<SeedInvoiceSale> InvoiceSales);

    private sealed record SeedAdjustment(string Id, string Month, string EmployeeId, decimal Amount, string Reason, string? ById);

    private sealed record SeedLock(string Kind, string Month, string BranchId, string? LockedById, DateTimeOffset LockedAt, decimal Total,
        DateTimeOffset? PaidAt);

    private sealed record SeedTier(decimal From, decimal Percent);

    private sealed record SeedTarget(string EmployeeId, string Month, decimal Target);

    private sealed record SeedInvoiceSale(string InvoiceId, string SaleId);
}
