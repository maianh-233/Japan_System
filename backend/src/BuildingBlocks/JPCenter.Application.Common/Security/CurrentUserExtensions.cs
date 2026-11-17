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
}
