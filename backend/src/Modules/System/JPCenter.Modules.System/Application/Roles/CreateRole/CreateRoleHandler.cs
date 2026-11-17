using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Roles.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Roles.CreateRole;

/// <summary>PQ-02: trùng tên (E1) · quyền vượt quá người tạo bị bỏ, báo số lượng (E2) · tự thêm Xem (PQ-03.E4).</summary>
public sealed class CreateRoleHandler(ISystemDbContext db, ICurrentUser user, IPermissionCatalog catalog, RoleReader reader, IAuditTrail audit)
    : ICommandHandler<CreateRoleCommand, RoleSaveResult>
{
    public async Task<Result<RoleSaveResult>> Handle(CreateRoleCommand cmd, CancellationToken ct)
    {
        var name = cmd.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name.ToLower() == name.ToLower(), ct)) return PermissionErrors.DuplicateRoleName;

        var wanted = new Dictionary<string, PermissionScope>();
        if (cmd.CopyFromRoleId is { } sourceId)
        {
            var source = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == new RoleId(sourceId), ct);
            if (source is null) return PermissionErrors.RoleNotFound;
            foreach (var (perm, scope) in RoleGrants.Of(source, catalog)) wanted[perm] = scope;
        }
        foreach (var (perm, scope) in cmd.Grants ?? [])
        {
            if (!catalog.IsValid(perm)) return PermissionErrors.UnknownPermission;
            wanted[perm] = RoleGrants.ParseScope(scope);
        }

        var checker = new GrantChecker(user.Access!);
        var grants = PermissionChanges.WithImpliedView(wanted, catalog);
        var kept = checker.Exceeding(grants);
        foreach (var perm in kept) grants.Remove(perm);

        var role = Role.Create(name, cmd.Description ?? "");
        if (role.IsFailure) return role.Error;
        role.Value.SetGrants(grants);
        db.Roles.Add(role.Value);
        await db.SaveChangesAsync(ct);

        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Tạo vai trò", name,
            $"{grants.Count} quyền{(kept.Count > 0 ? $"; bỏ {kept.Count} quyền vượt quá quyền người tạo" : "")}");
        return new RoleSaveResult(await reader.GetAsync(role.Value.Id, ct), kept, null,
            kept.Count > 0 ? $"{kept.Count} quyền vượt quá quyền của bạn — không sao chép" : null);
    }
}
