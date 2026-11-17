using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Nạp quyền hiệu lực cho middleware (đã cache 5 phút ở <c>UserAccessCache</c>).
/// Nhân viên: role + quyền lẻ (<see cref="EffectivePermissions"/>), tên / loại lấy từ Hr. Cổng HV / PH: không có mã quyền.
/// </summary>
internal sealed class UserAccessProvider(SystemDbContext db, IEmployeeDirectory employees, IClock clock) : IUserAccessProvider
{
    public Task<UserAccess?> LoadAsync(Guid accountId, bool isPortalAccount, CancellationToken ct) =>
        isPortalAccount ? LoadPortalAsync(accountId, ct) : LoadStaffAsync(accountId, ct);

    private async Task<UserAccess?> LoadStaffAsync(Guid accountId, CancellationToken ct)
    {
        var id = new UserAccountId(accountId);
        var account = await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) return null;

        var employee = await employees.FindAsync(account.EmployeeId, ct);
        var roleIds = account.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        var isSuper = EffectivePermissions.IsSuperAdmin(account.Roles, roles);
        var grants = EffectivePermissions.Compute(account.Roles, roles, account.Overrides, clock.UtcNow);
        var permissions = grants.Values
            .Where(g => g.UsableScope is not null)
            .ToDictionary(g => g.Permission, g => new EffectivePermission(g.UsableScope!.Value, g.BranchIds.Select(b => b.Value).ToList()));

        var roleNames = account.Roles.Select(r => roles.TryGetValue(r.RoleId, out var role) ? role.Name : null).OfType<string>().ToList();
        return new UserAccess(
            account.Id.Value,
            employee?.IsTeacher == true ? Portal.Teacher : Portal.Staff,
            account.TokenVersion,
            account.Status == UserAccountStatus.Active && employee?.HasQuit != true,
            employee?.FullName ?? account.Email,
            roleNames.Count > 0 ? string.Join(" · ", roleNames) : "Chưa có vai trò",
            account.EmployeeId,
            null,
            isSuper,
            account.Roles.Any(r => r.BranchIds.Count == 0),
            account.Roles.SelectMany(r => r.BranchIds).Select(b => b.Value).Distinct().ToList(),
            permissions);
    }

    private async Task<UserAccess?> LoadPortalAsync(Guid accountId, CancellationToken ct)
    {
        var id = new PortalAccountId(accountId);
        var account = await db.PortalAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) return null;

        var isStudent = account.OwnerType == PortalOwnerType.Student;
        return new UserAccess(
            account.Id.Value,
            isStudent ? Portal.Student : Portal.Parent,
            account.TokenVersion,
            account.Status == PortalAccountStatus.Active,
            account.DisplayName,
            isStudent ? "Học viên" : "Phụ huynh",
            null,
            account.OwnerId,
            false,
            false,
            [],
            new Dictionary<string, EffectivePermission>());
    }
}
