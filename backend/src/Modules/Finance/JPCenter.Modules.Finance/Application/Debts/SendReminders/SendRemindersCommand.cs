using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Debts.SendReminders;

/// <summary>
/// TC-11 bước 3 — nhắc học phí hàng loạt (1 request cho cả lô): ghi <c>payment_reminders</c> cho từng hóa đơn còn nợ trong phạm vi.
/// Hóa đơn hết nợ / ngoài phạm vi → bỏ qua, báo lại. Đã nhắc trong ngày → vẫn ghi, kèm cảnh báo (E2).
/// ⏳ Gửi thật qua Zalo / SMS / email theo mẫu <c>tuition</c> làm cùng module Thông báo.
/// </summary>
public sealed record SendRemindersCommand(IReadOnlyList<Guid> InvoiceIds, ReminderChannel Channel, string? Note) : ICommand<SendRemindersResult>;

/// <param name="Sent">Số hóa đơn đã ghi nhắc</param>
/// <param name="Skipped">Hóa đơn bỏ qua (hết nợ / đã hủy / ngoài phạm vi)</param>
public sealed record SendRemindersResult(int Sent, IReadOnlyList<Guid> Skipped, IReadOnlyList<string> Warnings);

public sealed class SendRemindersValidator : AbstractValidator<SendRemindersCommand>
{
    public SendRemindersValidator()
    {
        RuleFor(x => x.InvoiceIds).NotEmpty().WithMessage("Chọn hóa đơn cần nhắc").Must(x => x is null || x.Count <= 500);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class SendRemindersHandler(IFinanceDbContext db, FinanceAccess access, IClock clock)
    : ICommandHandler<SendRemindersCommand, SendRemindersResult>
{
    public async Task<Result<SendRemindersResult>> Handle(SendRemindersCommand cmd, CancellationToken ct)
    {
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        var ids = cmd.InvoiceIds.Distinct().Select(i => new InvoiceId(i)).ToList();
        var invoices = await db.Invoices.AsNoTracking().Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        var allowed = access.Branches(FinanceAccess.Debts, PermissionAction.Update);
        var now = clock.UtcNow;
        var today = clock.Today;
        var owing = invoices.Where(i => (allowed is null || allowed.Contains(i.BranchId.Value)) && i.Remaining > Money.Zero).ToList();

        var todayStart = VnTime.StartOf(today);
        var owingIds = owing.Select(i => i.Id).ToList();
        var already = await db.PaymentReminders.Where(r => owingIds.Contains(r.InvoiceId) && r.At >= todayStart).Select(r => r.InvoiceId).Distinct()
            .CountAsync(ct);
        foreach (var i in owing) db.PaymentReminders.Add(PaymentReminder.Manual(i.Id, cmd.Channel, actor, cmd.Note, now));
        await db.SaveChangesAsync(ct);

        var warnings = new List<string>();
        if (already > 0) warnings.Add($"{already} hóa đơn đã được nhắc trong hôm nay");
        var skipped = ids.Except(owingIds).Select(i => i.Value).ToList();
        if (skipped.Count > 0) warnings.Add($"Bỏ qua {skipped.Count} hóa đơn hết nợ / không trong phạm vi");
        return new SendRemindersResult(owing.Count, skipped, warnings);
    }
}
