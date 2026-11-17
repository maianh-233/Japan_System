using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Users.GetEffectivePermissions;

/// <summary>
/// PQ-07 — quyền hiệu lực của 1 người và nguồn gốc. Truyền <see cref="Roles"/> để XEM TRƯỚC kết quả nếu đổi vai trò (PQ-05 bước 3).
/// </summary>
public sealed record GetEffectivePermissionsQuery(Guid AccountId, List<RoleAssignmentInput>? Roles = null) : IQuery<EffectivePermissionsDto>;

/// <param name="Usable">Số quyền dùng được (không bị chặn)</param>
public sealed record EffectivePermissionsDto(bool SuperAdmin, int Usable, IReadOnlyList<EffectivePermissionDto> Permissions);

/// <param name="Scope">null khi chỉ có chặn lẻ</param>
/// <param name="BranchIds">Phạm vi chi nhánh — rỗng = mọi chi nhánh</param>
/// <param name="Sources">Tên role / "Cấp lẻ" (PQ-07.E1 liệt kê mọi nguồn)</param>
/// <param name="Denied">Bị chặn lẻ — hiện gạch ngang + lý do + hạn (PQ-07.E2)</param>
public sealed record EffectivePermissionDto(string Perm, string? Scope, IReadOnlyList<Guid> BranchIds, IReadOnlyList<string> Sources, DeniedDto? Denied);

public sealed record DeniedDto(string Reason, DateTimeOffset? Until);

public sealed class GetEffectivePermissionsHandler(ISystemDbContext db, IPermissionCatalog catalog, IClock clock)
    : IQueryHandler<GetEffectivePermissionsQuery, EffectivePermissionsDto>
{
    public async Task<Result<EffectivePermissionsDto>> Handle(GetEffectivePermissionsQuery query, CancellationToken ct)
    {
        var account = await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == new UserAccountId(query.AccountId), ct);
        if (account is null) return PermissionErrors.UserNotFound;
        var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);
        var assignments = query.Roles is null ? account.Roles : PermissionChanges.Preview(query.Roles);

        if (EffectivePermissions.IsSuperAdmin(assignments, roles))
        {
            var all = catalog.All.Order().Select(p => new EffectivePermissionDto(p, "all", [], ["Super Admin"], null)).ToList();
            return new EffectivePermissionsDto(true, all.Count, all);
        }

        // PQ-07.E3: quyền lẻ hết hạn không tính (Compute tự bỏ)
        var grants = EffectivePermissions.Compute(assignments, roles, account.Overrides, clock.UtcNow);
        var list = grants.Values.OrderBy(g => g.Permission, StringComparer.Ordinal)
            .Select(g => new EffectivePermissionDto(g.Permission, g.Scope is { } s ? RoleGrants.ScopeText(s) : null,
                g.BranchIds.Select(b => b.Value).ToList(), g.Sources.Distinct().ToList(),
                g.Denied is { } d ? new DeniedDto(d.Reason, d.Until) : null))
            .ToList();
        return new EffectivePermissionsDto(false, grants.Values.Count(g => g.UsableScope is not null), list);
    }
}
