using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Debts.AutoRemind;

/// <summary>
/// TC-11 bước 4 — tự nhắc (job hằng ngày): trước hạn 3 ngày, đúng hạn, quá hạn 7 ngày; mỗi mốc 1 lần cho mỗi hạn (đóng nhiều đợt: tính theo đợt đang áp dụng),
/// job lỡ ngày thì nhắc bù mốc mới nhất (<see cref="ReminderPlan"/>). Trả số hóa đơn đã nhắc.
/// ⏳ Gửi thật (Zalo / email theo mẫu <c>tuition</c>) làm cùng module Thông báo — hiện chỉ ghi lịch sử nhắc.
/// </summary>
public sealed record AutoRemindCommand : ICommand<int>;

public sealed class AutoRemindHandler(IFinanceDbContext db, IClock clock) : ICommandHandler<AutoRemindCommand, int>
{
    public static readonly ReminderPlan Plan = new();
    public const ReminderChannel Channel = ReminderChannel.Zalo;

    public async Task<Result<int>> Handle(AutoRemindCommand cmd, CancellationToken ct)
    {
        var today = clock.Today;
        var now = clock.UtcNow;
        var open = (await db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Open).ToListAsync(ct))
            .Select(i => (Invoice: i, Due: i.CurrentDueDate())).Where(x => x.Due is not null).ToList();
        var ids = open.Select(x => x.Invoice.Id).ToList();
        var auto = (await db.PaymentReminders.AsNoTracking().Where(r => ids.Contains(r.InvoiceId) && r.Milestone != null).ToListAsync(ct))
            .ToLookup(r => r.InvoiceId);

        var count = 0;
        foreach (var (invoice, due) in open)
        {
            // Mốc đã nhắc cho HẠN này: tính từ trước mốc "trước hạn" 1 ngày (đợt trước nhắc rồi không chặn đợt sau)
            var windowStart = VnTime.StartOf(due!.Value.AddDays(-Plan.DaysBefore - 1));
            var sent = auto[invoice.Id].Where(r => r.At >= windowStart).Select(r => r.Milestone!.Value).ToList();
            if (Plan.Due(due.Value, today, sent) is not { } milestone) continue;
            db.PaymentReminders.Add(PaymentReminder.Auto(invoice.Id, Channel, milestone, now));
            count++;
        }
        if (count > 0) await db.SaveChangesAsync(ct);
        return count;
    }
}
