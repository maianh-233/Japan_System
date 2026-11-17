using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Approvals.Common;

/// <summary>
/// PQ-09 bước 3 — áp dụng ĐÚNG thay đổi trong payload khi người thứ 2 duyệt.
/// Kiểm tra lại theo quyền HIỆN TẠI của người đề nghị: bị thu hẹp → không áp dụng (E4).
/// Người được cấp đã bị khóa → vẫn lưu, trả cảnh báo, không mở khóa (E5). Người gọi tự SaveChanges.
/// </summary>
public sealed class FinancePermissionApplier(
    ISystemDbContext db, IUserAccessProvider accessProvider, IPermissionCatalog catalog, UserEditGuard guard,
    PermissionChanges changes, IAuditTrail audit, IClock clock)
{
    public static readonly Error RequesterLacksRights = Error.Conflict("system.approval.requester_lacks_rights",
        "Người đề nghị không còn đủ quyền để cấp — yêu cầu không được áp dụng.");
    public static readonly Error TargetGone = Error.NotFound("system.approval.target_not_found",
        "Người dùng / vai trò trong yêu cầu không còn tồn tại.");

    /// <returns>Cảnh báo (nếu có) để hiển thị cho người duyệt.</returns>
    public async Task<Result<IReadOnlyList<string>>> ApplyAsync(ApprovalRequest request, CancellationToken ct)
    {
        var payload = ApprovalFactory.Read(request);
        var requester = await accessProvider.LoadAsync(request.RequestedById, false, ct);
        if (requester is null || !requester.IsActive) return RequesterLacksRights;

        var warnings = new List<string>();
        switch (payload.Type)
        {
            case FinancePermissionPayload.RoleGrants:
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == new RoleId(payload.RoleId!.Value), ct);
                if (role is null || role.IsSuperAdmin) return TargetGone;
                var checker = new GrantChecker(requester);
                if (payload.Grants!.Any(g => !catalog.IsValid(g.Key) || !checker.CanGrant(g.Key, RoleGrants.ParseScope(g.Value))))
                    return RequesterLacksRights;
                if (PermissionChanges.MergeGrants(role, payload.Grants!.ToDictionary(g => g.Key, g => (string?)g.Value)) is { IsFailure: true } f)
                    return f.Error;
                changes.Invalidate();
                audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Sửa quyền vai trò (đã duyệt)", role.Name,
                    $"{request.Code}: {string.Join(", ", payload.Grants!.Select(g => $"{g.Key} → {g.Value}"))}");
                break;
            }
            case FinancePermissionPayload.UserRoles:
            {
                var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.Id == new UserAccountId(payload.AccountId!.Value), ct);
                if (account is null) return TargetGone;
                var roles = await changes.RolesAsync(ct);
                var next = PermissionChanges.Preview(payload.Roles!);
                if (guard.CheckRoles(account.Roles, next, roles, requester).IsFailure) return RequesterLacksRights;
                changes.ApplyRoles(account, payload.Roles!);
                Warn(account, warnings);
                changes.Invalidate(account.Id.Value);
                audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Gán vai trò (đã duyệt)", payload.UserName ?? account.Email,
                    $"{request.Code}: {payload.Before} → {payload.After}");
                break;
            }
            case FinancePermissionPayload.UserOverride:
            {
                var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.Id == new UserAccountId(payload.AccountId!.Value), ct);
                if (account is null) return TargetGone;
                var input = payload.Override!;
                // Hạn đã qua trong lúc chờ duyệt → không còn ý nghĩa
                if (guard.CheckOverride(input, requester, clock.UtcNow) is { IsFailure: true } bad)
                    return bad.Error == PermissionErrors.ExceedsGranter ? RequesterLacksRights : bad.Error;
                changes.ApplyOverride(account, input, requester.AccountId);
                Warn(account, warnings);
                changes.Invalidate(account.Id.Value);
                audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Cấp quyền lẻ (đã duyệt)", payload.UserName ?? account.Email,
                    $"{request.Code}: {payload.After} — {input.Reason}");
                break;
            }
            default:
                return Error.Validation("system.approval.invalid_payload", "Nội dung yêu cầu không hợp lệ.");
        }
        return warnings;
    }

    private static void Warn(UserAccount account, List<string> warnings)
    {
        if (account.Status == UserAccountStatus.Locked)
            warnings.Add("Tài khoản đang bị khóa — đã lưu quyền nhưng không mở khóa.");
    }
}
