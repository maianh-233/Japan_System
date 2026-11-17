using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Users;

/// <summary>1 quyền sau khi cộng role + cấp lẻ và đánh dấu chặn lẻ (user story PQ-07).</summary>
/// <param name="BranchIds">Chi nhánh áp dụng khi phạm vi = Branch; rỗng = mọi chi nhánh.</param>
/// <param name="Sources">Quyền đến từ đâu: tên role / "Cấp lẻ".</param>
/// <param name="Denied">Chặn lẻ còn hạn — chặn luôn thắng.</param>
public sealed record EffectiveGrant(string Permission, PermissionScope? Scope, IReadOnlyList<BranchId> BranchIds,
    IReadOnlyList<string> Sources, PermissionOverride? Denied)
{
    /// <summary>Phạm vi thực dùng được (null = không có quyền / bị chặn)</summary>
    public PermissionScope? UsableScope => Denied is null ? Scope : null;
}

/// <summary>
/// Tính quyền hiệu lực — GIỐNG HỆT <c>frontend/src/features/system/access.ts › effectivePerms</c>:
/// hợp các role (lấy phạm vi rộng nhất; role gán vài chi nhánh thì "toàn hệ thống" thu về "chi nhánh", trừ module system)
/// + cấp lẻ còn hạn − chặn lẻ còn hạn.
/// </summary>
public static class EffectivePermissions
{
    /// <summary>Module chỉ có ý nghĩa toàn hệ thống — gán theo chi nhánh không thu hẹp phạm vi.</summary>
    public static bool IsGlobalOnly(string permission) => permission.StartsWith("system.", StringComparison.Ordinal);

    public static bool IsActive(DateTimeOffset? until, DateTimeOffset now) => until is null || until > now;

    /// <summary>Có role Super Admin → toàn quyền, bỏ qua ma trận.</summary>
    public static bool IsSuperAdmin(IEnumerable<UserRoleAssignment> assignments, IReadOnlyDictionary<RoleId, Role> roles) =>
        assignments.Any(a => roles.TryGetValue(a.RoleId, out var r) && r.IsSuperAdmin);

    public static IReadOnlyDictionary<string, EffectiveGrant> Compute(IReadOnlyCollection<UserRoleAssignment> assignments,
        IReadOnlyDictionary<RoleId, Role> roles, IEnumerable<PermissionOverride> overrides, DateTimeOffset now)
    {
        var acc = new Dictionary<string, Builder>(StringComparer.Ordinal);
        Builder Get(string p) => acc.TryGetValue(p, out var b) ? b : acc[p] = new Builder();

        foreach (var assignment in assignments)
        {
            if (!roles.TryGetValue(assignment.RoleId, out var role) || role.IsSuperAdmin) continue;
            foreach (var grant in role.Grants)
            {
                var scope = assignment.BranchIds.Count > 0 && grant.Scope == PermissionScope.All && !IsGlobalOnly(grant.Permission)
                    ? PermissionScope.Branch
                    : grant.Scope;
                var b = Get(grant.Permission);
                b.Widen(scope);
                b.Sources.Add(role.Name);
                if (assignment.BranchIds.Count == 0) b.AllBranches = true;
                else b.Branches.UnionWith(assignment.BranchIds);
            }
        }

        var userBranches = assignments.SelectMany(a => a.BranchIds).ToHashSet();
        var userAllBranches = assignments.Any(a => a.BranchIds.Count == 0);
        foreach (var o in overrides.Where(o => IsActive(o.Until, now)))
        {
            var b = Get(o.Permission);
            if (o.Effect == OverrideEffect.Deny)
            {
                b.Denied = o;
                continue;
            }
            b.Widen(o.Scope ?? PermissionScope.Own);
            b.Sources.Add("Cấp lẻ");
            if (userAllBranches) b.AllBranches = true;
            else b.Branches.UnionWith(userBranches);
        }

        return acc.ToDictionary(kv => kv.Key, kv => new EffectiveGrant(kv.Key, kv.Value.Scope,
            kv.Value.AllBranches ? [] : kv.Value.Branches.ToList(), kv.Value.Sources, kv.Value.Denied));
    }

    private sealed class Builder
    {
        public PermissionScope? Scope;
        public bool AllBranches;
        public readonly HashSet<BranchId> Branches = [];
        public readonly List<string> Sources = [];
        public PermissionOverride? Denied;

        public void Widen(PermissionScope scope)
        {
            if (Scope is null || scope > Scope) Scope = scope;
        }
    }
}
