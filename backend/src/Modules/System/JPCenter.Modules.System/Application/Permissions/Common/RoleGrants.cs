using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Permissions.Common;

/// <summary>Tiện ích quanh ma trận quyền.</summary>
public static class RoleGrants
{
    /// <summary>Quyền của role — Super Admin = mọi quyền trong danh mục, phạm vi toàn hệ thống (giống <c>roleGrants()</c> frontend).</summary>
    public static IReadOnlyDictionary<string, PermissionScope> Of(Role role, IPermissionCatalog catalog) => role.IsSuperAdmin
        ? catalog.All.ToDictionary(p => p, _ => PermissionScope.All)
        : role.Grants.ToDictionary(g => g.Permission, g => g.Scope);

    public static bool IsFinance(string permission) => permission.StartsWith("finance.", StringComparison.Ordinal);

    /// <summary>
    /// Quyền tài chính người dùng SẼ CÓ THÊM nếu áp dụng thay đổi (so quyền hiệu lực trước / sau) — có thì phải duyệt 2 người (PQ-09).
    /// Super Admin sau thay đổi = có thêm mọi quyền tài chính chưa có.
    /// </summary>
    public static IReadOnlyList<string> FinanceAdded(
        IReadOnlyDictionary<string, EffectiveGrant> before, bool superBefore,
        IReadOnlyDictionary<string, EffectiveGrant> after, bool superAfter, IPermissionCatalog catalog)
    {
        if (superBefore) return [];
        var had = before.Values.Where(g => g.UsableScope is not null && IsFinance(g.Permission))
            .ToDictionary(g => g.Permission, g => g.UsableScope!.Value);
        var now = superAfter
            ? catalog.All.Where(IsFinance).ToDictionary(p => p, _ => PermissionScope.All)
            : after.Values.Where(g => g.UsableScope is not null && IsFinance(g.Permission)).ToDictionary(g => g.Permission, g => g.UsableScope!.Value);
        return now.Where(kv => !had.TryGetValue(kv.Key, out var old) || kv.Value > old).Select(kv => kv.Key).Order().ToList();
    }

    public static string ScopeText(PermissionScope scope) => scope switch
    {
        PermissionScope.Own => "own",
        PermissionScope.Branch => "branch",
        _ => "all",
    };

    public static PermissionScope ParseScope(string scope) => scope switch
    {
        "own" => PermissionScope.Own,
        "branch" => PermissionScope.Branch,
        "all" => PermissionScope.All,
        _ => throw new FormatException($"Phạm vi không hợp lệ: {scope}"),
    };

    public static bool IsScope(string? scope) => scope is "own" or "branch" or "all";
}
