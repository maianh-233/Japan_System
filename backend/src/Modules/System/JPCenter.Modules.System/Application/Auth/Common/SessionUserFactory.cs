using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>Dựng <see cref="SessionUserDto"/> từ quyền đã nạp — logic menu giống <c>access.ts › menuPermissions</c>.</summary>
public static class SessionUserFactory
{
    public static SessionUserDto Create(UserAccess access, string email, bool mustChangePassword)
    {
        var portal = access.Portal.ToString().ToLowerInvariant();
        IReadOnlyList<string> menus = access.Portal switch
        {
            Portal.Teacher => ["teacher.*"],
            Portal.Student => ["student.*"],
            Portal.Parent => ["parent.*"],
            _ when access.IsSuperAdmin => ["*"],
            _ => access.Permissions.Keys
                .Where(k => k.EndsWith(".view", StringComparison.Ordinal))
                .Select(k => k[..^".view".Length])
                .Order(StringComparer.Ordinal)
                .ToList(),
        };

        // GV có thêm quyền nhân viên (vd kiêm Giáo vụ) → được chuyển sang cổng nhân viên
        var portals = access.Portal == Portal.Teacher && (access.IsSuperAdmin || access.Permissions.Count > 0)
            ? new[] { "teacher", "staff" }
            : new[] { portal };

        var isPortalAccount = access.Portal is Portal.Student or Portal.Parent;
        return new SessionUserDto(
            access.EmployeeId ?? access.AccountId,
            access.AccountId,
            email,
            access.DisplayName,
            null,
            portal,
            access.RoleName,
            access.IsSuperAdmin || access.AllBranches ? "global" : "branch",
            access.BranchIds,
            isPortalAccount ? access.AccountId : null,
            access.OwnerId,
            menus,
            access.Permissions.ToDictionary(kv => kv.Key, kv => ScopeText(kv.Value.Scope)),
            portals,
            mustChangePassword);
    }

    private static string ScopeText(PermissionScope scope) => scope switch
    {
        PermissionScope.Own => "own",
        PermissionScope.Branch => "branch",
        _ => "all",
    };
}
