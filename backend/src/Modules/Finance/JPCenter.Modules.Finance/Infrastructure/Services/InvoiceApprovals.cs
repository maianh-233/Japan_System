using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IInvoiceApprovals"/>: phiếu ở hộp duyệt chung → yêu cầu trên hóa đơn (<c>invoice_requests.approval_id</c>).
/// Duyệt hoàn → ghi hoàn (người tạo + người duyệt); duyệt hủy → hóa đơn "đã hủy", trả lại lượt mã ưu đãi (TC-03.E7). Người duyệt phải có
/// <c>finance.invoices.approve</c> tại chi nhánh của hóa đơn.
/// </summary>
public sealed class InvoiceApprovals(FinanceDbContext db, FinanceAccess access, IClock clock) : IInvoiceApprovals
{
    public async Task<Result> DecideAsync(Guid approvalId, bool approve, string? note, CancellationToken ct)
    {
        var found = await FindAsync(approvalId, ct);
        if (found.IsFailure) return found.Error;
        var (invoice, request) = found.Value;
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        if (!access.InBranch(FinanceAccess.Invoices, PermissionAction.Approve, invoice.BranchId.Value)) return FinanceAccess.BranchNotAllowed;

        var now = clock.UtcNow;
        var decided = approve ? invoice.ApproveRequest(request.Id, actor, now) : invoice.RejectRequest(request.Id, actor, note, now);
        if (decided.IsFailure) return decided.Error;
        if (approve && request.Kind == InvoiceRequestKind.Void && invoice.DiscountId is { } discountId
            && await db.Discounts.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == discountId, ct) is { } discount)
            discount.Release();
        invoice.ClearDomainEvents();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> WithdrawAsync(Guid approvalId, CancellationToken ct)
    {
        var found = await FindAsync(approvalId, ct);
        if (found.IsFailure) return found.Error;
        var (invoice, request) = found.Value;
        var withdrawn = invoice.WithdrawRequest(request.Id, access.ActorId ?? Guid.Empty, clock.UtcNow);
        if (withdrawn.IsFailure) return withdrawn.Error;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result<(Invoice, InvoiceRequest)>> FindAsync(Guid approvalId, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Requests.Any(r => r.ApprovalId == approvalId), ct);
        var request = invoice?.Requests.FirstOrDefault(r => r.ApprovalId == approvalId);
        return invoice is null || request is null ? InvoiceErrors.RequestNotFound : (invoice, request);
    }
}
