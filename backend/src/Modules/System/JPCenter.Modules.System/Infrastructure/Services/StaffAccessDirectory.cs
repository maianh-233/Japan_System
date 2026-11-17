using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IStaffAccessDirectory"/>: tính quyền hiệu lực (giống kiểm tra quyền khi gọi API) của mọi tài khoản đang hoạt động.
/// Số tài khoản nhân viên nhỏ (vài trăm) → tính trong bộ nhớ là đủ.
/// </summary>
internal sealed class StaffAccessDirectory(SystemDbContext db, IClock clock) : IStaffAccessDirectory
{
    public async Task<IReadOnlyList<StaffContact>> WithPermissionAsync(string permission, Guid? branchId, CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);
        var accounts = await db.UserAccounts.AsNoTracking().Where(a => a.Status == UserAccountStatus.Active).ToListAsync(ct);
        var now = clock.UtcNow;
        var branch = branchId is { } id ? new BranchId(id) : (BranchId?)null;

        return accounts.Where(a =>
            {
                if (EffectivePermissions.IsSuperAdmin(a.Roles, roles)) return true;
                var grants = EffectivePermissions.Compute(a.Roles, roles, a.Overrides, now);
                if (!grants.TryGetValue(permission, out var g) || g.UsableScope is null) return false;
                // Rỗng = mọi chi nhánh
                return branch is null || g.BranchIds.Count == 0 || g.BranchIds.Contains(branch.Value);
            })
            .Select(a => new StaffContact(a.Id.Value, a.EmployeeId, a.Email))
            .ToList();
    }

    public async Task<bool> IsLastActiveSuperAdminAsync(Guid employeeId, CancellationToken ct)
    {
        var superRoleIds = await db.Roles.Where(r => r.IsSuperAdmin).Select(r => r.Id).ToListAsync(ct);
        var supers = await db.UserAccounts.AsNoTracking()
            .Where(a => a.Status == UserAccountStatus.Active && a.Roles.Any(r => superRoleIds.Contains(r.RoleId)))
            .Select(a => a.EmployeeId).ToListAsync(ct);
        return supers.Count == 1 && supers[0] == employeeId;
    }
}
