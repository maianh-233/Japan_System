using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Users.SetUserRoles;

/// <summary>
/// PQ-05: không tự sửa mình (E1) · role vượt quá người gán (E2) · chi nhánh ngoài phạm vi người gán (E5) ·
/// gộp role trùng (E4, domain) · có thêm quyền tài chính → gửi duyệt, chưa áp dụng (E6) · luôn còn ≥ 1 Super Admin (E8).
/// </summary>
public sealed class SetUserRolesHandler(
    ISystemDbContext db, ICurrentUser user, UserEditGuard guard, SuperAdminGuard superAdmins, PermissionChanges changes,
    ApprovalFactory approvals, IEmployeeDirectory employees, UserReader reader, IAuditTrail audit)
    : ICommandHandler<SetUserRolesCommand, UserChangeResult>
{
    public async Task<Result<UserChangeResult>> Handle(SetUserRolesCommand cmd, CancellationToken ct)
    {
        var editor = user.Access!;
        var target = await guard.LoadTargetAsync(cmd.AccountId, editor, ct);
        if (target.IsFailure) return target.Error;
        var account = target.Value;

        var roles = await changes.RolesAsync(ct);
        var next = PermissionChanges.Preview(cmd.Roles);
        if (guard.CheckRoles(account.Roles, next, roles, editor) is { IsFailure: true } denied) return denied.Error;

        var wasSuper = SuperAdminGuard.HasSuperRole(account.Roles.Select(r => r.RoleId), roles);
        var staysSuper = SuperAdminGuard.HasSuperRole(next.Select(r => r.RoleId), roles);
        if (wasSuper && !staysSuper && account.Status == UserAccountStatus.Active && await superAdmins.WouldLeaveNoneAsync(account.Id, ct))
            return PermissionErrors.LastSuperAdmin;

        var name = (await employees.FindAsync(account.EmployeeId, ct))?.FullName ?? account.Email;
        var before = await guard.DescribeAsync(account.Roles, roles, ct);
        var after = await guard.DescribeAsync(next, roles, ct);

        // PQ-05.E6: có thêm quyền tài chính → chờ người thứ 2 duyệt
        var financeAdded = changes.FinanceAdded(account, roles, next, account.Overrides);
        if (financeAdded.Count > 0)
        {
            var request = await approvals.FinancePermissionAsync(editor, $"Gán vai trò cho {name}", $"{before} → {after}",
                new FinancePermissionPayload(FinancePermissionPayload.UserRoles, account.Id.Value, name, cmd.Roles, null, null, null, null,
                    financeAdded.ToList(), before, after), ct);
            await db.SaveChangesAsync(ct);
            audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Gửi duyệt gán vai trò có quyền tài chính", name, $"{before} → {after}");
            return new UserChangeResult(await reader.GetAsync(account.Id, ct), request.Id.Value,
                "Đã gửi yêu cầu duyệt — vai trò có quyền tài chính chỉ áp dụng khi người thứ 2 duyệt.", []);
        }

        changes.ApplyRoles(account, cmd.Roles);
        await db.SaveChangesAsync(ct);
        changes.Invalidate(account.Id.Value);
        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Gán vai trò", name, $"{before} → {after}");
        return new UserChangeResult(await reader.GetAsync(account.Id, ct), null, null, []);
    }
}
