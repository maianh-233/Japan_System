using System.Text.Json;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Approvals.Common;

/// <summary>Đọc yêu cầu duyệt + quyết định ai được duyệt loại nào (giống <c>canApproveKind</c> frontend).</summary>
public sealed class ApprovalReader(ISystemDbContext db)
{
    /// <summary>
    /// Mọi loại cần quyền trang Duyệt thao tác (danh mục chỉ có view…export, không có approve). Thêm theo loại:
    /// Cấp quyền tài chính: người duyệt là cấp quản trị (xem được Nhật ký hoặc Phân quyền người dùng).
    /// Hoàn tiền / hủy hóa đơn: cần quyền Hóa đơn (module Finance — chưa làm).
    /// </summary>
    public static bool CanApproveKind(ApprovalKind kind, ICurrentUser user) =>
        user.Can("system.approvals", PermissionAction.View) && (kind == ApprovalKind.FinancePermission
            ? user.Can("system.audit", PermissionAction.View) || user.Can("system.user-permissions", PermissionAction.View)
            : user.Can("finance.invoices", PermissionAction.View));

    public async Task<IReadOnlyList<ApprovalDto>> ListAsync(ICurrentUser user, string? status, CancellationToken ct, ApprovalRequestId? only = null)
    {
        var query = db.ApprovalRequests.AsNoTracking();
        if (only is { } id) query = query.Where(x => x.Id == id);
        if (!string.IsNullOrEmpty(status))
        {
            var parsed = Enum.GetValues<ApprovalStatus>().FirstOrDefault(v => StatusText(v) == status);
            query = query.Where(x => x.Status == parsed);
        }
        var list = await query.OrderByDescending(x => x.RequestedAt).ToListAsync(ct);
        var me = user.AccountId;
        // Thấy: yêu cầu mình tạo + loại mình duyệt được
        return list.Where(x => x.RequestedById == me || CanApproveKind(x.Kind, user))
            .Select(x => ToDto(x, user)).ToList();
    }

    public async Task<ApprovalDto> GetAsync(ApprovalRequestId id, ICurrentUser user, CancellationToken ct) =>
        (await ListAsync(user, null, ct, id)).Single();

    private static ApprovalDto ToDto(ApprovalRequest x, ICurrentUser user)
    {
        var pending = x.Status == ApprovalStatus.Pending;
        using var payload = JsonDocument.Parse(x.Payload);
        return new ApprovalDto(x.Id.Value, x.Code, KindText(x.Kind), x.Title, x.Detail, x.BranchId?.Value,
            payload.RootElement.Clone(), new PersonDto(x.RequestedById, x.RequestedByName), x.RequestedAt,
            StatusText(x.Status),
            x.DecidedById is { } by ? new PersonDto(by, x.DecidedByName ?? "") : null, x.DecidedAt, x.Note,
            pending && x.RequestedById != user.AccountId && CanApproveKind(x.Kind, user),
            pending && x.RequestedById == user.AccountId);
    }

    public static string KindText(ApprovalKind kind) => kind switch
    {
        ApprovalKind.Refund => "refund",
        ApprovalKind.VoidInvoice => "void_invoice",
        _ => "finance_permission",
    };

    public static string StatusText(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending => "pending",
        ApprovalStatus.Approved => "approved",
        ApprovalStatus.Rejected => "rejected",
        _ => "withdrawn",
    };
}
