using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Students.Contracts.IntegrationEvents;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Invoices.EventHandlers;

/// <summary>
/// Ghi danh kết thúc vì nghỉ / chuyển lớp (lớp hủy → Students cho nghỉ) → hủy phần học phí còn lại của hóa đơn: hết công nợ, tiền đã thu giữ nguyên
/// để hoàn / khấu trừ. Học xong (completed) → không đổi. Hóa đơn đã hủy / đã hủy phần còn lại → bỏ qua (chạy lại an toàn).
/// </summary>
public sealed class CancelInvoiceOnEnrollmentEnded(IFinanceDbContext db, IClock clock) : IIntegrationEventHandler<EnrollmentEnded>
{
    public async Task Handle(EnrollmentEnded e, CancellationToken ct)
    {
        if (e.Status is not ("dropped" or "transferred")) return;
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.EnrollmentId == e.EnrollmentId && i.Status == InvoiceStatus.Open, ct);
        if (invoice is null) return;

        var reason = e.Status == "transferred" ? "Chuyển lớp" : "Học viên nghỉ";
        if (!string.IsNullOrWhiteSpace(e.Reason)) reason += $": {e.Reason.Trim()}";
        if (invoice.CancelRemaining(reason, clock.UtcNow).IsFailure) return;
        invoice.ClearDomainEvents();
        await db.SaveChangesAsync(ct);
    }
}
