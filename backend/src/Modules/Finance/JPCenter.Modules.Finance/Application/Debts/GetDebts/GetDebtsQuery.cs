using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Debts.GetDebts;

/// <summary>
/// TC-11 bước 1–2 — công nợ: mỗi hóa đơn còn nợ 1 dòng (hạn đang áp dụng, tuổi nợ, người trả = người liên hệ chính của HV — PH gom các con;
/// không có → chính HV), lần nhắc gần nhất, đã nhắc hôm nay (E2). Kèm tổng theo tuổi nợ. Phạm vi chi nhánh của <c>finance.debts</c> (E4).
/// </summary>
public sealed record GetDebtsQuery(Guid? BranchId, Guid? ClassId) : IQuery<DebtsDto>;

/// <param name="Aging">not_due · days1_to30 · days31_to60 · over60</param>
public sealed record DebtDto(Guid InvoiceId, string Code, Guid StudentId, string StudentName, Guid ClassId, string? ClassCode, Guid BranchId,
    decimal Total, decimal Remaining, DateOnly DueDate, int OverdueDays, DebtAging Aging, DebtPayerDto Payer, ReminderDto? LastReminder,
    bool RemindedToday);

/// <param name="LeadId">Crm.Lead của người trả (PH); null = chính học viên</param>
public sealed record DebtPayerDto(Guid? LeadId, string Name, string? Phone);

public sealed record DebtAgingTotals(decimal NotDue, decimal Days1To30, decimal Days31To60, decimal Over60);

public sealed record DebtsDto(IReadOnlyList<DebtDto> Items, decimal Total, DebtAgingTotals Aging);

public sealed class GetDebtsHandler(
    IFinanceDbContext db, FinanceAccess access, IEnrollmentLookup students, IClassLookup classes, IGuardianDirectory guardians, PeopleNames people,
    IClock clock) : IQueryHandler<GetDebtsQuery, DebtsDto>
{
    public async Task<Result<DebtsDto>> Handle(GetDebtsQuery q, CancellationToken ct)
    {
        var query = db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Open);
        if (access.Branches(FinanceAccess.Debts, PermissionAction.View) is { } allowed)
        {
            var ids = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(i => ids.Contains(i.BranchId));
        }
        if (q.BranchId is { } branch) query = query.Where(i => i.BranchId == new BranchId(branch));
        if (q.ClassId is { } cls) query = query.Where(i => i.ClassId == cls);
        var today = clock.Today;
        var owing = (await query.ToListAsync(ct)).Select(i => (Invoice: i, Summary: i.Summarize(today))).Where(x => x.Summary.Remaining > Money.Zero).ToList();

        var invoiceIds = owing.Select(x => x.Invoice.Id).ToList();
        var last = (await db.PaymentReminders.AsNoTracking().Where(r => invoiceIds.Contains(r.InvoiceId)).ToListAsync(ct))
            .GroupBy(r => r.InvoiceId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.At).ToList());
        var studentIds = owing.Select(x => x.Invoice.StudentId).Distinct().ToList();
        var names = await students.NamesAsync(studentIds, ct);
        var payers = await guardians.DecidersOfAsync(studentIds, ct);
        var cls2 = await classes.FindManyAsync(owing.Select(x => x.Invoice.ClassId).Distinct().ToList(), ct);
        var who = await people.OfAsync(last.Values.SelectMany(l => l.Select(r => r.By)), ct);
        var todayStart = VnTime.StartOf(today);

        var items = owing.Select(x =>
        {
            var i = x.Invoice;
            var due = x.Summary.CurrentDueDate ?? i.DueDate;
            var studentName = names.GetValueOrDefault(i.StudentId) ?? "—";
            var payer = payers.TryGetValue(i.StudentId, out var g) && g.Relationship != "self"
                ? new DebtPayerDto(g.LeadId, g.FullName, g.Phone)
                : new DebtPayerDto(g?.LeadId, studentName, g?.Phone);
            var reminders = last.GetValueOrDefault(i.Id) ?? [];
            return new DebtDto(i.Id.Value, i.Code, i.StudentId, studentName, i.ClassId, cls2.TryGetValue(i.ClassId, out var c) ? c.Code : null,
                i.BranchId.Value, x.Summary.Total.Amount, x.Summary.Remaining.Amount, due, Math.Max(0, today.DayNumber - due.DayNumber),
                DebtAgingRule.Of(due, today), payer,
                reminders.Count > 0 ? InvoiceReader.Reminder(reminders[0], id => id is { } k ? who.GetValueOrDefault(k) ?? "—" : "—") : null,
                reminders.Any(r => r.At >= todayStart));
        }).OrderByDescending(d => d.OverdueDays).ThenBy(d => d.DueDate).ToList();

        decimal Sum(DebtAging a) => items.Where(d => d.Aging == a).Sum(d => d.Remaining);
        return new DebtsDto(items, items.Sum(d => d.Remaining),
            new DebtAgingTotals(Sum(DebtAging.NotDue), Sum(DebtAging.Days1To30), Sum(DebtAging.Days31To60), Sum(DebtAging.Over60)));
    }
}
