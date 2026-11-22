using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Debts.AutoRemind;
using JPCenter.Modules.Finance.Application.Debts.GetDebts;
using JPCenter.Modules.Finance.Application.Debts.SendReminders;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>TC-11 — công nợ & nhắc học phí (<c>finance.debts.*</c>)</summary>
public static class DebtEndpoints
{
    public sealed record RemindBody(List<Guid>? InvoiceIds, ReminderChannel Channel, string? Note);

    public static void Map(IEndpointRouteBuilder finance)
    {
        const string page = FinanceAccess.Debts;

        finance.MapGet("/debts", async (Guid? branchId, Guid? classId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetDebtsQuery(branchId, classId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // 1 request cho cả lô
        finance.MapPost("/reminders", async (RemindBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SendRemindersCommand(b.InvoiceIds ?? [], b.Channel, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Nhắc học phí", AuditCategory.Finance);

        // Chạy tay job tự nhắc (thường chạy 08:00 hằng ngày)
        finance.MapPost("/reminders/auto", async (ISender s, CancellationToken ct) => (await s.Send(new AutoRemindCommand(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Chạy tự nhắc học phí", AuditCategory.Finance);
    }
}
