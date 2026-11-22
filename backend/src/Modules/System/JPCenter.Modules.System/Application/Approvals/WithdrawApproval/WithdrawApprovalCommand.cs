using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Approvals.DecideApproval;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Approvals.WithdrawApproval;

/// <summary>PQ-09.E2, TC-07.E3 — người tạo rút lại yêu cầu khi chưa ai duyệt.</summary>
public sealed record WithdrawApprovalCommand(Guid RequestId) : ICommand<ApprovalDto>;

public sealed class WithdrawApprovalHandler(
    ISystemDbContext db, ICurrentUser user, IInvoiceApprovals invoices, ApprovalReader reader, IAuditTrail audit, IClock clock)
    : ICommandHandler<WithdrawApprovalCommand, ApprovalDto>
{
    public async Task<Result<ApprovalDto>> Handle(WithdrawApprovalCommand cmd, CancellationToken ct)
    {
        var request = await db.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == new ApprovalRequestId(cmd.RequestId), ct);
        if (request is null) return DecideApprovalHandler.NotFound;
        if (request.Withdraw(user.AccountId ?? Guid.Empty, clock.UtcNow) is { IsFailure: true } failed) return failed.Error;
        // Hoàn tiền / hủy hóa đơn: bỏ khoản "đang chờ" trên hóa đơn (TC-07.E3)
        if (request.Kind != ApprovalKind.FinancePermission && await invoices.WithdrawAsync(request.Id.Value, ct) is { IsFailure: true } f) return f.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Permission, AuditSeverity.Info, "Rút lại yêu cầu", $"{request.Code} · {request.Title}");
        return await reader.GetAsync(request.Id, user, ct);
    }
}
