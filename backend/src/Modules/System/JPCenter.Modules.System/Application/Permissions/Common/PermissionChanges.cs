using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Permissions.Common;

/// <summary>
/// Áp dụng thay đổi quyền — dùng chung khi lưu thẳng và khi người thứ 2 duyệt (PQ-09 bước 3). Người gọi tự SaveChanges
/// rồi gọi <see cref="Invalidate"/> để quyền mới có hiệu lực ngay.
/// </summary>
public sealed class PermissionChanges(ISystemDbContext db, IPermissionCatalog catalog, IUserAccessCache cache, IClock clock)
{
    public async Task<IReadOnlyDictionary<RoleId, Role>> RolesAsync(CancellationToken ct) =>
        await db.Roles.ToDictionaryAsync(r => r.Id, ct);

    /// <summary>Quyền hiệu lực trước / sau khi đổi vai trò hoặc thêm quyền lẻ — để biết có thêm quyền tài chính không.</summary>
    public IReadOnlyList<string> FinanceAdded(UserAccount account, IReadOnlyDictionary<RoleId, Role> roles,
        IReadOnlyCollection<UserRoleAssignment> newRoles, IEnumerable<PermissionOverride> newOverrides)
    {
        var now = clock.UtcNow;
        var before = EffectivePermissions.Compute(account.Roles, roles, account.Overrides, now);
        var after = EffectivePermissions.Compute(newRoles, roles, newOverrides, now);
        return RoleGrants.FinanceAdded(before, EffectivePermissions.IsSuperAdmin(account.Roles, roles),
            after, EffectivePermissions.IsSuperAdmin(newRoles, roles), catalog);
    }

    public static IReadOnlyCollection<UserRoleAssignment> Preview(IEnumerable<RoleAssignmentInput> roles)
    {
        // Dùng chính logic gộp của domain trên 1 tài khoản tạm
        var temp = UserAccount.Invite(Guid.Empty, "preview@local").Value;
        temp.SetRoles(roles.Select(r => (new RoleId(r.RoleId), (IReadOnlyCollection<BranchId>)r.BranchIds.Select(b => new BranchId(b)).ToList())));
        return temp.Roles;
    }

    public void ApplyRoles(UserAccount account, IEnumerable<RoleAssignmentInput> roles) =>
        account.SetRoles(roles.Select(r => (new RoleId(r.RoleId), (IReadOnlyCollection<BranchId>)r.BranchIds.Select(b => new BranchId(b)).ToList())));

    public PermissionOverride ApplyOverride(UserAccount account, OverrideInput input, Guid by) =>
        account.AddOverride(input.Permission, input.Effect == "deny" ? OverrideEffect.Deny : OverrideEffect.Grant,
            input.Effect == "deny" ? null : RoleGrants.ParseScope(input.Scope ?? "own"), input.Until, input.Reason.Trim(), by, clock.UtcNow);

    /// <summary>Gộp các ô quyền vào ma trận hiện có của role (ô rỗng = bỏ quyền).</summary>
    public static Result MergeGrants(Role role, IReadOnlyDictionary<string, string?> cells)
    {
        var grants = role.Grants.ToDictionary(g => g.Permission, g => g.Scope);
        foreach (var (perm, scope) in cells)
        {
            if (scope is null) grants.Remove(perm);
            else grants[perm] = RoleGrants.ParseScope(scope);
        }
        return role.SetGrants(grants);
    }

    public void Invalidate(Guid? accountId = null)
    {
        if (accountId is { } id) cache.Invalidate(id);
        else cache.InvalidateAll();
    }

    /// <summary>PQ-03.E4: có quyền thêm / sửa / xóa / duyệt / xuất mà chưa có Xem cùng trang → tự thêm Xem cùng phạm vi.</summary>
    public static Dictionary<string, PermissionScope> WithImpliedView(IReadOnlyDictionary<string, PermissionScope> grants, IPermissionCatalog catalog)
    {
        var result = new Dictionary<string, PermissionScope>(grants);
        foreach (var (perm, scope) in grants)
        {
            if (perm.EndsWith(".view", StringComparison.Ordinal)) continue;
            var view = perm[..perm.LastIndexOf('.')] + ".view";
            if (!catalog.IsValid(view)) continue;
            if (!result.TryGetValue(view, out var existing) || existing < scope) result[view] = scope;
        }
        return result;
    }
}
