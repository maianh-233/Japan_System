using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Users.Common;

/// <summary>Đọc tài khoản nhân viên kèm tên (từ Hr) và tên người cấp quyền lẻ.</summary>
public sealed class UserReader(ISystemDbContext db, IEmployeeDirectory employees, IClock clock)
{
    public async Task<IReadOnlyList<UserAccessDto>> ListAsync(CancellationToken ct, UserAccountId? only = null)
    {
        var query = db.UserAccounts.AsNoTracking();
        if (only is { } id) query = query.Where(a => a.Id == id);
        var accounts = await query.ToListAsync(ct);

        // Người cấp quyền lẻ là tài khoản → đổi ra nhân viên để lấy tên
        var granterIds = accounts.SelectMany(a => a.Overrides.Select(o => new UserAccountId(o.GrantedBy))).Distinct().ToList();
        var granters = await db.UserAccounts.AsNoTracking().Where(a => granterIds.Contains(a.Id))
            .Select(a => new { a.Id, a.EmployeeId }).ToDictionaryAsync(a => a.Id.Value, a => a.EmployeeId, ct);
        var names = await employees.FindManyAsync(accounts.Select(a => a.EmployeeId).Concat(granters.Values).ToList(), ct);

        var now = clock.UtcNow;
        return accounts
            .Select(a => ToDto(a, names.GetValueOrDefault(a.EmployeeId), o =>
                granters.TryGetValue(o, out var emp) && names.TryGetValue(emp, out var e) ? e.FullName : "Hệ thống", now))
            .OrderBy(u => u.Name, StringComparer.Create(new global::System.Globalization.CultureInfo("vi-VN"), false))
            .ToList();
    }

    public async Task<UserAccessDto> GetAsync(UserAccountId id, CancellationToken ct) => (await ListAsync(ct, id)).Single();

    private static UserAccessDto ToDto(UserAccount a, EmployeeSummary? e, Func<Guid, string> granterName, DateTimeOffset now) => new(
        a.Id.Value, a.EmployeeId, e?.FullName ?? a.Email, e?.Code, e?.Position, e?.BranchId, a.Email, StatusText(a.Status),
        a.Roles.Select(r => new RoleAssignmentDto(r.RoleId.Value, r.BranchIds.Select(b => b.Value).ToList())).ToList(),
        a.Overrides.OrderByDescending(o => o.GrantedAt).Select(o => new PermOverrideDto(o.Id, o.Permission,
            o.Effect == OverrideEffect.Deny ? "deny" : "grant", o.Scope is { } s ? RoleGrants.ScopeText(s) : null,
            o.Until, o.Reason, granterName(o.GrantedBy), o.GrantedAt, !EffectivePermissions.IsActive(o.Until, now))).ToList(),
        a.LastLoginAt, a.LockedReason);

    public static string StatusText(UserAccountStatus status) => status switch
    {
        UserAccountStatus.Invited => "invited",
        UserAccountStatus.Locked => "locked",
        _ => "active",
    };
}
