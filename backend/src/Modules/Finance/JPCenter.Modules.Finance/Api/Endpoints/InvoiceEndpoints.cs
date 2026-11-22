using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Idempotency;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Invoices.GetInvoice;
using JPCenter.Modules.Finance.Application.Invoices.GetInvoices;
using JPCenter.Modules.Finance.Application.Invoices.RecordPayment;
using JPCenter.Modules.Finance.Application.Invoices.RequestRefund;
using JPCenter.Modules.Finance.Application.Invoices.RequestVoid;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>
/// TC-05 → TC-08 — hóa đơn (<c>finance.invoices.*</c>). Hóa đơn chỉ sinh từ ghi danh (không có POST tạo). Duyệt hoàn / hủy:
/// <c>POST /api/v1/system/approvals/{id}/approve|reject</c> (hộp duyệt chung, cần <c>finance.invoices.approve</c>).
/// </summary>
public static class InvoiceEndpoints
{
    public sealed record PaymentBody(decimal Amount, PaymentMethod Method, DateTimeOffset? PaidAt, string? Note);

    public sealed record RefundBody(decimal Amount, PaymentMethod Method, string? Reason);

    public sealed record VoidBody(string? Reason);

    public static void Map(IEndpointRouteBuilder finance)
    {
        const string page = FinanceAccess.Invoices;
        var g = finance.MapGroup("/invoices");

        g.MapGet("/", async (QueryEnum<TuitionStatus>? status, Guid? branchId, Guid? classId, Guid? studentId, DateOnly? from, DateOnly? to, string? q,
                    ISender s, CancellationToken ct) =>
                (await s.Send(new GetInvoicesQuery(status?.Value, branchId, classId, studentId, from, to, q), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetInvoiceQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // TC-06.E4 — bấm 2 lần không ghi 2 khoản
        g.MapPost("/{id:guid}/payments", async (Guid id, PaymentBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RecordPaymentCommand(id, b.Amount, b.Method, b.PaidAt, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Idempotent()
            .Audited("Thu tiền", AuditCategory.Finance, AuditSeverity.Info);

        g.MapPost("/{id:guid}/refunds", async (Guid id, RefundBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RequestRefundCommand(id, b.Amount, b.Method, b.Reason), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Idempotent()
            .Audited("Hoàn tiền", AuditCategory.Finance, AuditSeverity.Warning);

        g.MapPost("/{id:guid}/void-requests", async (Guid id, VoidBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RequestVoidCommand(id, b.Reason), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Đề nghị hủy hóa đơn", AuditCategory.Finance, AuditSeverity.Warning);
    }
}
