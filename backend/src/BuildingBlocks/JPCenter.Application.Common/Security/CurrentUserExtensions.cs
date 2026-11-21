using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Security;

public static class CurrentUserExtensions
{
    /// <summary>"crm.leads" + Update → "crm.leads.update"</summary>
    public static string PermissionCode(string page, PermissionAction action) => $"{page}.{action.ToString().ToLowerInvariant()}";

    /// <summary>Phạm vi của quyền; null = không có quyền. Super Admin = toàn hệ thống.</summary>
    public static EffectivePermission? Permission(this ICurrentUser user, string page, PermissionAction action)
    {
        var access = user.Access;
        if (access is null) return null;
        if (access.IsSuperAdmin) return new EffectivePermission(PermissionScope.All, []);
        return access.Permissions.GetValueOrDefault(PermissionCode(page, action));
    }

    public static bool Can(this ICurrentUser user, string page, PermissionAction action) => user.Permission(page, action) is not null;

    /// <summary>
    /// Chi nhánh được thao tác theo quyền (PQ-10): null = mọi chi nhánh · rỗng = không có quyền. Phạm vi <c>branch</c> / <c>own</c> lấy
    /// chi nhánh gán cho vai trò, không gán riêng thì theo chi nhánh của người dùng.
    /// </summary>
    public static HashSet<Guid>? AllowedBranches(this ICurrentUser user, string page, PermissionAction action)
    {
        var perm = user.Permission(page, action);
        if (perm is null) return [];
        if (perm.Scope == PermissionScope.All) return null;
        if (perm.BranchIds.Count > 0) return perm.BranchIds.ToHashSet();
        var me = user.Access;
        return me is null || me.AllBranches ? null : me.BranchIds.ToHashSet();
    }

    /// <summary>Chi nhánh <paramref name="branchId"/> nằm trong phạm vi quyền</summary>
    public static bool InBranch(this ICurrentUser user, string page, PermissionAction action, Guid branchId) =>
        user.AllowedBranches(page, action) is not { } allowed || allowed.Contains(branchId);
}
