using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Roles.DeleteRole;

/// <summary>PQ-04 — xóa vai trò → Thùng rác (khôi phục trong số ngày ở Cài đặt chung › Giữ trong Thùng rác).</summary>
public sealed record DeleteRoleCommand(Guid RoleId) : ICommand;

/// <summary>Role mặc định không xóa (E1); đang được gán (kể cả tài khoản khóa) → báo danh sách người dùng (E2).</summary>
public sealed class DeleteRoleHandler(ISystemDbContext db, ICurrentUser user, IEmployeeDirectory employees, PermissionChanges changes,
    IAuditTrail audit, IClock clock, SettingsStore settings) : ICommandHandler<DeleteRoleCommand>
{
    public async Task<Result> Handle(DeleteRoleCommand cmd, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == new RoleId(cmd.RoleId), ct);
        if (role is null) return PermissionErrors.RoleNotFound;
        if (role.EnsureDeletable() is { IsFailure: true } system) return system;

        var users = await db.UserAccounts.AsNoTracking().Where(a => a.Roles.Any(r => r.RoleId == role.Id)).Select(a => a.EmployeeId).ToListAsync(ct);
        if (users.Count > 0)
        {
            var names = await employees.FindManyAsync(users, ct);
            return PermissionErrors.RoleInUse(users.Select(id => names.TryGetValue(id, out var e) ? e.FullName : id.ToString()));
        }

        var trashDays = (await settings.GetAsync(ct)).Safety.TrashDays;
        var trash = TrashItem.Create("system.role", role.Id.Value, role.Name, role.Description, null,
            user.AccountId ?? Guid.Empty, clock.UtcNow, trashDays);
        if (trash.IsFailure) return trash.Error;

        db.Roles.Remove(role); // xóa mềm (interceptor)
        db.TrashItems.Add(trash.Value);
        await db.SaveChangesAsync(ct);
        changes.Invalidate();

        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Xóa vai trò", role.Name, $"Vào Thùng rác — khôi phục được trong {trashDays} ngày");
        return Result.Success();
    }
}
