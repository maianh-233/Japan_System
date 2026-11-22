using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IApprovalInbox"/> — mở phiếu hoàn tiền / hủy hóa đơn cho Finance (người tạo = người đang gọi).</summary>
public sealed class ApprovalInbox(ISystemDbContext db, ApprovalFactory factory, ICurrentUser user, IAuditTrail audit) : IApprovalInbox
{
    public async Task<ApprovalTicket> OpenAsync(ApprovalOpening opening, CancellationToken ct)
    {
        var kind = opening.Kind switch
        {
            "refund" => ApprovalKind.Refund,
            "void_invoice" => ApprovalKind.VoidInvoice,
            _ => throw new ArgumentOutOfRangeException(nameof(opening), opening.Kind, "Loại phiếu duyệt không hỗ trợ"),
        };
        var requester = user.Access ?? throw new InvalidOperationException("Mở phiếu duyệt cần người dùng đã đăng nhập");
        var request = await factory.CreateAsync(requester, kind, opening.Title, opening.Detail,
            opening.BranchId is { } b ? new BranchId(b) : null, opening.Payload, ct);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Finance, AuditSeverity.Warning, "Gửi yêu cầu duyệt", $"{request.Code} · {request.Title}", opening.Detail);
        return new ApprovalTicket(request.Id.Value, request.Code);
    }
}
