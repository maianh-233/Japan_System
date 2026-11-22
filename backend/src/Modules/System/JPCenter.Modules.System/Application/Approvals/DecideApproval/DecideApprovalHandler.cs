using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Approvals.DecideApproval;

/// <summary>
/// Tự duyệt (E1) · đã xử lý rồi (E3) — domain kiểm. Không đủ quyền duyệt loại này → 403.
/// Cấp quyền tài chính → <see cref="FinancePermissionApplier"/> (kiểm lại quyền người đề nghị — E4, cảnh báo khóa — E5).
/// Hoàn tiền / hủy hóa đơn → Finance ghi hoàn / hủy (<see cref="IInvoiceApprovals"/>, kiểm lại nghiệp vụ; lỗi → phiếu giữ nguyên chờ duyệt).
/// </summary>
public sealed class DecideApprovalHandler(
    ISystemDbContext db, ICurrentUser user, FinancePermissionApplier applier, IInvoiceApprovals invoices, ApprovalReader reader, IAuditTrail audit,
    IClock clock)
    : ICommandHandler<DecideApprovalCommand, ApprovalDecisionResult>
{
    public static readonly Error NotAllowed = Error.Forbidden("system.approval.not_allowed", "Bạn không có quyền duyệt loại yêu cầu này.");
    public static readonly Error NotFound = Error.NotFound("system.approval.not_found", "Không tìm thấy yêu cầu duyệt.");

    public async Task<Result<ApprovalDecisionResult>> Handle(DecideApprovalCommand cmd, CancellationToken ct)
    {
        var request = await db.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == new ApprovalRequestId(cmd.RequestId), ct);
        if (request is null) return NotFound;
        var me = user.Access!;
        var now = clock.UtcNow;

        // Tự duyệt / đã xử lý báo trước cả khi thiếu quyền — thông điệp rõ hơn
        var decided = cmd.Approve
            ? request.Approve(me.AccountId, me.DisplayName, cmd.Note?.Trim(), now)
            : request.Reject(me.AccountId, me.DisplayName, cmd.Note!.Trim(), now);
        if (decided.IsFailure) return decided.Error;
        if (!ApprovalReader.CanApproveKind(request.Kind, user)) return NotAllowed;

        IReadOnlyList<string> warnings = [];
        if (request.Kind != ApprovalKind.FinancePermission)
        {
            // Finance tự lưu hoàn / hủy trước; thất bại → không lưu quyết định ở đây
            if (await invoices.DecideAsync(request.Id.Value, cmd.Approve, cmd.Note?.Trim(), ct) is { IsFailure: true } failed) return failed.Error;
        }
        else if (cmd.Approve)
        {
            var applied = await applier.ApplyAsync(request, ct);
            if (applied.IsFailure) return applied.Error;
            warnings = applied.Value;
        }

        await db.SaveChangesAsync(ct);
        audit.Record(request.Kind == ApprovalKind.FinancePermission ? AuditCategory.Permission : AuditCategory.Finance, AuditSeverity.Warning, cmd.Approve ? "Duyệt yêu cầu" : "Từ chối yêu cầu",
            $"{request.Code} · {request.Title}", cmd.Note);
        // Báo người đề nghị (chuông thông báo) — làm khi có module Notifications
        return new ApprovalDecisionResult(await reader.GetAsync(request.Id, user, ct), warnings);
    }
}
