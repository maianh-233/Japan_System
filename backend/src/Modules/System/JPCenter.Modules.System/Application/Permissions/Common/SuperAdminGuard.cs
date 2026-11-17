using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Permissions.Common;

/// <summary>PQ-05.E8, PQ-08.E5 — hệ thống luôn còn ≥ 1 Super Admin đang hoạt động.</summary>
public sealed class SuperAdminGuard(ISystemDbContext db)
{
    /// <summary>Nếu tài khoản <paramref name="accountId"/> mất quyền Super Admin (gỡ role / bị khóa) thì còn ai không?</summary>
    public async Task<bool> WouldLeaveNoneAsync(UserAccountId accountId, CancellationToken ct)
    {
        var superRoleIds = await db.Roles.Where(r => r.IsSuperAdmin).Select(r => r.Id).ToListAsync(ct);
        var others = await db.UserAccounts
            .Where(a => a.Id != accountId && a.Status == UserAccountStatus.Active)
            .Where(a => a.Roles.Any(r => superRoleIds.Contains(r.RoleId)))
            .AnyAsync(ct);
        return !others;
    }

    public static bool HasSuperRole(IEnumerable<RoleId> roleIds, IReadOnlyDictionary<RoleId, Role> roles) =>
        roleIds.Any(id => roles.TryGetValue(id, out var r) && r.IsSuperAdmin);
}
