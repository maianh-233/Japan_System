using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Roles.Common;

/// <summary>Đọc role kèm số người dùng, lần sửa cuối (người sửa = tên nhân viên), version.</summary>
public sealed class RoleReader(ISystemDbContext db, IEmployeeDirectory employees)
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct, RoleId? only = null)
    {
        var query = db.Roles.AsNoTracking();
        if (only is { } id) query = query.Where(r => r.Id == id);
        var rows = await query
            .Select(r => new
            {
                Role = r,
                UpdatedAt = EF.Property<DateTimeOffset?>(r, "UpdatedAt") ?? EF.Property<DateTimeOffset>(r, "CreatedAt"),
                UpdatedBy = EF.Property<Guid?>(r, "UpdatedBy") ?? EF.Property<Guid?>(r, "CreatedBy"),
                Version = EF.Property<uint>(r, "xmin"),
            })
            .ToListAsync(ct);

        var accounts = await db.UserAccounts.AsNoTracking().ToListAsync(ct);
        var usage = accounts.Where(a => a.Status != UserAccountStatus.Locked)
            .SelectMany(a => a.Roles.Select(r => r.RoleId))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var editorAccounts = accounts.ToDictionary(a => a.Id.Value, a => a.EmployeeId);
        var editorIds = rows.Select(r => r.UpdatedBy).OfType<Guid>().Where(editorAccounts.ContainsKey).Select(id => editorAccounts[id]).ToList();
        var names = await employees.FindManyAsync(editorIds, ct);

        return rows
            .OrderBy(r => r.Role.IsSystem ? 0 : 1).ThenBy(r => r.Role.Name)
            .Select(r => new RoleDto(
                r.Role.Id.Value, r.Role.Name, r.Role.Description, r.Role.IsSystem, r.Role.IsSuperAdmin,
                r.Role.Grants.ToDictionary(g => g.Permission, g => RoleGrants.ScopeText(g.Scope)),
                usage.GetValueOrDefault(r.Role.Id),
                r.UpdatedAt,
                r.UpdatedBy is { } by && editorAccounts.TryGetValue(by, out var emp) && names.TryGetValue(emp, out var e) ? e.FullName : null,
                r.Version))
            .ToList();
    }

    public async Task<RoleDto> GetAsync(RoleId id, CancellationToken ct) => (await ListAsync(ct, id)).Single();
}
