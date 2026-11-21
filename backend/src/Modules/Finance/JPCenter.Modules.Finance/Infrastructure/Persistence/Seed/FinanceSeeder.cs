using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp dữ liệu mẫu Tài chính từ <c>finance.seed.json</c> (xuất từ mock frontend). Mỗi bảng chỉ nạp khi CÒN TRỐNG, không xóa gì.
/// Mọi thay đổi đi qua method domain: lập hóa đơn → thu → hoàn (có người duyệt thì tạo yêu cầu rồi duyệt) → hủy phần còn lại.
/// Bộ đếm số hóa đơn đặt bằng số lớn nhất đã dùng theo chi nhánh + tháng (mẫu mặc định đánh số lại hàng tháng).
/// </summary>
internal sealed class FinanceSeeder(FinanceDbContext db, ILogger<FinanceSeeder> logger) : IDataSeeder
{
    public int Order => 50;

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<FinanceSeedFile>(typeof(FinanceSeeder).Assembly, "finance.seed.json");
        var counts = new List<string>();

        if (!await db.BranchPrices.AnyAsync(ct)) counts.Add($"{SeedBranchPrices(data)} giá theo chi nhánh");
        if (!await db.Discounts.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedDiscounts(data)} ưu đãi");
        if (!await db.InstallmentPlans.AnyAsync(ct)) counts.Add($"{SeedPlans(data)} chính sách đóng đợt");
        if (!await db.BankTransactions.AnyAsync(ct)) counts.Add($"{SeedBankTransactions(data)} giao dịch ngân hàng");
        if (!await db.Invoices.IgnoreQueryFilters().AnyAsync(ct))
        {
            counts.Add($"{SeedInvoices(data)} hóa đơn");
            if (!await db.InvoiceCounters.AnyAsync(ct)) counts.Add($"{SeedCounters(data)} bộ đếm số hóa đơn");
            if (!await db.PaymentReminders.AnyAsync(ct)) counts.Add($"{SeedReminders(data)} lần nhắc học phí");
        }

        if (counts.Count == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dữ liệu mẫu Finance: {Counts}", string.Join(", ", counts));
    }

    // ------------------------------------------------------------------ Bảng giá, ưu đãi, đóng đợt

    private int SeedBranchPrices(FinanceSeedFile data)
    {
        var added = 0;
        foreach (var p in data.BranchPrices)
        {
            var created = BranchPrice.Create(SeedData.IdOf(p.CourseId), Branch(p.BranchId), Money.FromTrusted(p.Fee));
            if (Skip(created, $"{p.CourseId}/{p.BranchId}")) continue;
            db.Add(created.Value);
            added++;
        }
        return added;
    }

    private int SeedDiscounts(FinanceSeedFile data)
    {
        var added = 0;
        foreach (var d in data.Discounts)
        {
            var created = Discount.Create(new DiscountDetails(d.Code, d.Name, Enum<DiscountKind>(d.Kind), d.Value,
                d.CourseIds.Select(SeedData.IdOf).ToList(), d.BranchIds.Select(Branch).ToList(), DateOnly.Parse(d.From), DateOnly.Parse(d.To),
                d.Limit, d.Note));
            if (Skip(created, d.Code)) continue;
            var discount = created.Value;
            for (var i = 0; i < d.Used; i++) discount.Use();
            discount.SetActive(d.Active);
            db.AddWithId(discount, SeedData.IdOf(d.Id));
            added++;
        }
        return added;
    }

    /// <summary>Mock có "Đóng 1 lần" (1 đợt 100%) — domain yêu cầu ≥ 2 đợt (1 lần là mặc định, không cần chính sách) nên bỏ qua</summary>
    private int SeedPlans(FinanceSeedFile data)
    {
        var added = 0;
        foreach (var p in data.InstallmentPlans)
        {
            var created = InstallmentPlan.Create(p.Name, p.Parts.Select(x => new InstallmentPart(x.Percent, x.OffsetDays)).ToList(),
                Money.FromTrusted(p.MinFee), p.Surcharge);
            if (Skip(created, p.Name)) continue;
            created.Value.SetActive(p.Active);
            db.AddWithId(created.Value, SeedData.IdOf(p.Id));
            added++;
        }
        return added;
    }

    // ------------------------------------------------------------------ Giao dịch ngân hàng

    private int SeedBankTransactions(FinanceSeedFile data)
    {
        var added = 0;
        foreach (var t in data.BankTransactions)
        {
            var created = BankTransaction.Receive(t.Ref, t.At, Money.FromTrusted(t.Amount), t.Content, t.Payer, t.Account, Branch(t.BranchId));
            if (Skip(created, t.Ref)) continue;
            var txn = created.Value;
            var result = t.Status switch
            {
                "auto" => txn.Match(Invoices(t.InvoiceIds), null, t.MatchedAt ?? t.At),
                "matched" => txn.Match(Invoices(t.InvoiceIds), Person(t.MatchedById), t.MatchedAt ?? t.At),
                "ignored" => txn.Ignore(t.Note ?? "Không phải học phí"),
                _ => Result.Success(),
            };
            if (SkipResult(result, t.Ref)) continue;
            db.AddWithId(txn, SeedData.IdOf(t.Id));
            db.SetCreatedAt(txn, t.At);
            added++;
        }
        return added;
    }

    // ------------------------------------------------------------------ Hóa đơn

    private int SeedInvoices(FinanceSeedFile data)
    {
        var added = 0;
        foreach (var i in data.Invoices)
        {
            var created = Invoice.Issue(i.Code, SeedData.IdOf(i.EnrollmentId), SeedData.IdOf(i.StudentId), SeedData.IdOf(i.ClassId),
                Branch(i.BranchId), Money.FromTrusted(i.Amount), Money.FromTrusted(i.Discount), null, DateOnly.Parse(i.DueDate),
                InvoiceCode.TransferContent(i.Code, i.StudentName), i.IssuedAt);
            if (Skip(created, i.Code)) continue;
            var invoice = created.Value;
            if (!Replay(invoice, i)) continue;

            invoice.ClearDomainEvents();
            db.AddWithId(invoice, SeedData.IdOf(i.Id));
            db.SetCreatedAt(invoice, i.IssuedAt);
            added++;
        }
        return added;
    }

    /// <summary>Thu → hoàn → hủy phần còn lại, theo đúng thứ tự nghiệp vụ (mock có hoàn ghi ngày trước lần thu — vẫn chạy theo thứ tự)</summary>
    private bool Replay(Invoice invoice, SeedInvoice i)
    {
        foreach (var p in i.Payments)
        {
            var paid = invoice.Pay(Money.FromTrusted(p.Amount), Enum<PaymentMethod>(p.Method), p.PaidAt, Person(p.CollectorId),
                p.BankTransactionId is null ? null : SeedData.IdOf(p.BankTransactionId));
            if (Skip(paid, $"{i.Code} thu {p.Id}")) return false;
            SetId(paid.Value, new PaymentId(SeedData.IdOf(p.Id)));
        }

        foreach (var r in i.Refunds)
        {
            var by = Person(r.ById) ?? Guid.Empty;
            var outcome = invoice.RequestRefund(Money.FromTrusted(r.Amount), Enum<PaymentMethod>(r.Method), r.Reason, by,
                r.ApprovedById is not null, r.RefundedAt);
            if (Skip(outcome, $"{i.Code} hoàn {r.Id}")) return false;
            if (outcome.Value.Request is { } request
                && SkipResult(invoice.ApproveRequest(request.Id, SeedData.IdOf(r.ApprovedById!), r.RefundedAt), $"{i.Code} duyệt hoàn {r.Id}"))
                return false;
            if (invoice.Refunds.LastOrDefault() is { } refund) SetId(refund, new RefundId(SeedData.IdOf(r.Id)));
        }

        if (i.Cancelled)
        {
            var at = i.Payments.Select(p => p.PaidAt).Concat(i.Refunds.Select(r => r.RefundedAt)).DefaultIfEmpty(i.IssuedAt).Max();
            if (SkipResult(invoice.CancelRemaining(i.CancelReason ?? "Học viên nghỉ học", at), $"{i.Code} hủy phần còn lại")) return false;
        }
        return true;
    }

    /// <summary>Bộ đếm theo chi nhánh + tháng = số thứ tự lớn nhất trong mã mẫu (HD-{CN}-{YY}{MM}-{SO:4})</summary>
    private int SeedCounters(FinanceSeedFile data)
    {
        var counters = data.Invoices
            .Select(i => (i.BranchId, Parts: i.Code.Split('-')))
            .Where(x => x.Parts.Length == 4 && x.Parts[2].Length == 4 && int.TryParse(x.Parts[3], out _))
            .GroupBy(x => (x.BranchId, Period: $"20{x.Parts[2][..2]}-{x.Parts[2][2..]}"))
            .Select(g => InvoiceCounter.Start(Branch(g.Key.BranchId), g.Key.Period, g.Max(x => int.Parse(x.Parts[3]))))
            .ToList();
        db.InvoiceCounters.AddRange(counters);
        return counters.Count;
    }

    // ------------------------------------------------------------------ Nhắc học phí

    /// <summary>Tự nhắc (không có người nhắc) → mốc tính theo hạn hóa đơn: trước hạn / đúng hạn / quá hạn</summary>
    private int SeedReminders(FinanceSeedFile data)
    {
        var dueDates = data.Invoices.ToDictionary(i => i.Id, i => DateOnly.Parse(i.DueDate));
        var invoiceIds = db.ChangeTracker.Entries<Invoice>().Select(e => e.Entity.Id).ToHashSet();
        var added = 0;
        foreach (var r in data.Reminders)
        {
            var invoiceId = new InvoiceId(SeedData.IdOf(r.InvoiceId));
            if (!invoiceIds.Contains(invoiceId) || !dueDates.TryGetValue(r.InvoiceId, out var due)) continue;
            var channel = Enum<ReminderChannel>(r.Channel);
            var reminder = r.ById is { } by
                ? PaymentReminder.Manual(invoiceId, channel, SeedData.IdOf(by), r.Note, r.At)
                : PaymentReminder.Auto(invoiceId, channel, MilestoneOf(due, DateOnly.FromDateTime(r.At.UtcDateTime)), r.At);
            db.AddWithId(reminder, SeedData.IdOf(r.Id));
            added++;
        }
        return added;
    }

    private static ReminderMilestone MilestoneOf(DateOnly due, DateOnly day) =>
        day < due ? ReminderMilestone.BeforeDue : day == due ? ReminderMilestone.OnDue : ReminderMilestone.Overdue;

    // ------------------------------------------------------------------ Tiện ích

    private bool Skip<T>(Result<T> result, string label) => SkipResult(result, label);

    private bool SkipResult(Result result, string label)
    {
        if (result.IsSuccess) return false;
        logger.LogWarning("Bỏ qua dữ liệu mẫu Finance {Label}: {Error}", label, result.Error.Message);
        return true;
    }

    /// <summary>Id cố định cho entity con (khoản thu / hoàn) — factory domain tự sinh Id. Chỉ dùng khi nạp dữ liệu mẫu.</summary>
    private static void SetId<TId>(Entity<TId> entity, TId id) where TId : struct =>
        typeof(Entity<TId>).GetProperty(nameof(Entity<TId>.Id))!.SetValue(entity, id);

    private static T Enum<T>(string value) where T : struct, global::System.Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private static BranchId Branch(string mockId) => new(SeedData.IdOf(mockId));

    private static Guid? Person(string? mockId) => mockId is null ? null : SeedData.IdOf(mockId);

    private static List<InvoiceId> Invoices(List<string> mockIds) => mockIds.Select(x => new InvoiceId(SeedData.IdOf(x))).ToList();

    // ------------------------------------------------------------------ Định dạng file

    private sealed record FinanceSeedFile(List<SeedBranchPrice> BranchPrices, List<SeedDiscount> Discounts, List<SeedPlan> InstallmentPlans,
        List<SeedInvoice> Invoices, List<SeedBankTransaction> BankTransactions, List<SeedReminder> Reminders);

    private sealed record SeedBranchPrice(string CourseId, string BranchId, decimal Fee);

    private sealed record SeedDiscount(string Id, string Code, string Name, string Kind, decimal Value, List<string> CourseIds,
        List<string> BranchIds, string From, string To, int? Limit, int Used, bool Active, string? Note);

    private sealed record SeedPart(decimal Percent, int OffsetDays);

    private sealed record SeedPlan(string Id, string Name, List<SeedPart> Parts, decimal MinFee, decimal Surcharge, bool Active);

    private sealed record SeedPayment(string Id, decimal Amount, string Method, DateTimeOffset PaidAt, string? CollectorId, string? BankTransactionId);

    private sealed record SeedRefund(string Id, decimal Amount, string Method, DateTimeOffset RefundedAt, string Reason, string? ById,
        string? ApprovedById);

    private sealed record SeedInvoice(string Id, string Code, string EnrollmentId, string StudentId, string StudentName, string ClassId,
        string BranchId, decimal Amount, decimal Discount, string DueDate, DateTimeOffset IssuedAt, List<SeedPayment> Payments,
        List<SeedRefund> Refunds, bool Cancelled, string? CancelReason);

    private sealed record SeedBankTransaction(string Id, string Ref, DateTimeOffset At, decimal Amount, string Content, string Payer,
        string Account, string BranchId, string Status, List<string> InvoiceIds, string? MatchedById, DateTimeOffset? MatchedAt, string? Note);

    private sealed record SeedReminder(string Id, string InvoiceId, string Channel, DateTimeOffset At, string? ById, string? Note);
}
