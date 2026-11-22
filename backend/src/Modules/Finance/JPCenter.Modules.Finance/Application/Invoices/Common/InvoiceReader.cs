using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Invoices.Common;

/// <summary>Dựng <see cref="InvoiceDto"/> cho nhiều hóa đơn: tên HV (Students), lớp (Academic), tên người (Hr), lịch sử nhắc — mỗi nguồn 1 lần.</summary>
public sealed class InvoiceReader(IFinanceDbContext db, IEnrollmentLookup students, IClassLookup classes, PeopleNames people, IClock clock)
{
    public const string AutoCollector = "Tự động (VietQR)";

    public async Task<IReadOnlyList<InvoiceDto>> ToDtosAsync(IReadOnlyList<Invoice> invoices, CancellationToken ct)
    {
        if (invoices.Count == 0) return [];
        var ids = invoices.Select(i => i.Id).ToList();
        var reminders = await db.PaymentReminders.AsNoTracking().Where(r => ids.Contains(r.InvoiceId)).OrderByDescending(r => r.At).ToListAsync(ct);
        var names = await students.NamesAsync(invoices.Select(i => i.StudentId).Distinct().ToList(), ct);
        var cls = await classes.FindManyAsync(invoices.Select(i => i.ClassId).Distinct().ToList(), ct);
        var who = await people.OfAsync(invoices.SelectMany(i =>
                i.Payments.Select(p => p.CollectorId)
                    .Concat(i.Refunds.SelectMany(r => new[] { (Guid?)r.By, r.ApprovedBy }))
                    .Concat(i.Requests.SelectMany(r => new[] { (Guid?)r.RequestedBy, r.DecidedBy }))
                    .Concat([i.VoidRequestedBy, i.VoidApprovedBy]))
            .Concat(reminders.Select(r => r.By)), ct);
        var today = clock.Today;
        var byInvoice = reminders.ToLookup(r => r.InvoiceId);

        string Name(Guid? id) => id is { } g && who.TryGetValue(g, out var n) ? n : "—";

        return invoices.Select(i =>
        {
            var s = i.Summarize(today);
            cls.TryGetValue(i.ClassId, out var c);
            return new InvoiceDto(i.Id.Value, i.Code, i.EnrollmentId, i.StudentId, names.GetValueOrDefault(i.StudentId) ?? "—", i.ClassId, c?.Code,
                c?.Name, i.BranchId.Value, i.Amount.Amount, i.Discount.Amount, i.DiscountId?.Value, i.InstallmentPlanId?.Value,
                i.Schedule.Select(x => new InstallmentDueDto(x.Amount.Amount, x.DueDate)).ToList(), s.CurrentDueDate ?? i.DueDate, i.IssuedAt,
                i.TransferContent, s.Total.Amount, s.Collected.Amount, s.Refunded.Amount, s.Paid.Amount, s.Remaining.Amount, s.Refundable.Amount,
                s.Status,
                i.Payments.OrderBy(p => p.PaidAt).Select(p => new PaymentDto(p.Id.Value, p.Amount.Amount, p.Method, p.PaidAt,
                    p.CollectorId is null ? AutoCollector : Name(p.CollectorId), p.CollectorId, p.BankTransactionId, p.Note)).ToList(),
                i.Refunds.OrderBy(r => r.RefundedAt).Select(r => new RefundDto(r.Id.Value, r.Amount.Amount, r.Method, r.RefundedAt, r.Reason,
                    Name(r.By), r.ApprovedBy is null ? null : Name(r.ApprovedBy))).ToList(),
                i.CancelledAt is not null, i.CancelReason,
                i.Status == InvoiceStatus.Voided ? new VoidedDto(i.VoidReason ?? "", Name(i.VoidRequestedBy), Name(i.VoidApprovedBy), i.VoidedAt!.Value) : null,
                i.Requests.OrderByDescending(r => r.RequestedAt).Select(r => new InvoiceRequestDto(r.Id.Value, r.Kind, r.Amount?.Amount, r.Method,
                    r.Reason, Name(r.RequestedBy), r.RequestedAt, r.Status, r.ApprovalId, r.DecidedBy is null ? null : Name(r.DecidedBy), r.DecidedAt,
                    r.DecisionNote)).ToList(),
                byInvoice[i.Id].Select(r => Reminder(r, Name)).ToList());
        }).ToList();
    }

    public async Task<InvoiceDto> ToDtoAsync(Invoice invoice, CancellationToken ct) => (await ToDtosAsync([invoice], ct))[0];

    /// <summary>Hình thức thu / hoàn dạng chữ như JSON (nội dung phiếu duyệt — frontend đọc <c>method</c>)</summary>
    public static string MethodText(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "cash",
        PaymentMethod.Transfer => "transfer",
        PaymentMethod.Vietqr => "vietqr",
        _ => "card",
    };

    public static ReminderDto Reminder(PaymentReminder r, Func<Guid?, string> name) =>
        new(r.Id.Value, r.InvoiceId.Value, r.Channel, r.At, r.By is null ? null : name(r.By), r.Note, r.Milestone);
}
