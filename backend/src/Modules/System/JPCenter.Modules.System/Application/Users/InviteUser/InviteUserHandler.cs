using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Users.InviteUser;

/// <summary>
/// PQ-08: vai trò vượt quá người cấp (E1) · chưa có email (E2) · email trùng (E3) · đã có tài khoản / đã nghỉ việc.
/// Vai trò ban đầu có quyền tài chính → tài khoản vẫn được tạo + mời, còn vai trò thì chờ duyệt 2 người (PQ-09).
/// </summary>
public sealed class InviteUserHandler(
    ISystemDbContext db, ICurrentUser user, IEmployeeDirectory employees, UserEditGuard guard, PermissionChanges changes,
    ApprovalFactory approvals, InvitationMailer mailer, UserReader reader, IAuditTrail audit)
    : ICommandHandler<InviteUserCommand, UserChangeResult>
{
    public async Task<Result<UserChangeResult>> Handle(InviteUserCommand cmd, CancellationToken ct)
    {
        var editor = user.Access!;
        var employee = await employees.FindAsync(cmd.EmployeeId, ct);
        if (employee is null) return PermissionErrors.EmployeeNotFound;
        if (employee.HasQuit) return PermissionErrors.EmployeeQuit;
        if (string.IsNullOrWhiteSpace(employee.Email)) return PermissionErrors.EmployeeNoEmail;
        if (await db.UserAccounts.AnyAsync(a => a.EmployeeId == employee.Id, ct)) return PermissionErrors.EmployeeHasAccount;
        var email = employee.Email.Trim().ToLowerInvariant();
        if (await db.UserAccounts.AnyAsync(a => a.Email == email, ct)) return PermissionErrors.DuplicateEmail;

        var roles = await changes.RolesAsync(ct);
        var next = PermissionChanges.Preview(cmd.Roles);
        if (guard.CheckRoles([], next, roles, editor) is { IsFailure: true } denied) return denied.Error;

        var created = UserAccount.Invite(employee.Id, email);
        if (created.IsFailure) return created.Error;
        var account = created.Value;
        db.UserAccounts.Add(account);

        var after = await guard.DescribeAsync(next, roles, ct);
        var financeAdded = changes.FinanceAdded(account, roles, next, []);
        Guid? approvalId = null;
        if (financeAdded.Count > 0)
        {
            var request = await approvals.FinancePermissionAsync(editor, $"Vai trò ban đầu cho {employee.FullName}", after,
                new FinancePermissionPayload(FinancePermissionPayload.UserRoles, account.Id.Value, employee.FullName, cmd.Roles, null, null, null, null,
                    financeAdded.ToList(), "(tài khoản mới)", after), ct);
            approvalId = request.Id.Value;
        }
        else
        {
            changes.ApplyRoles(account, cmd.Roles);
        }

        await mailer.SendAsync(account, ct); // lưu tài khoản + token mời rồi gửi mail
        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Cấp tài khoản", employee.FullName,
            $"{email} · {after}{(approvalId is null ? "" : " (vai trò chờ duyệt)")}");
        return new UserChangeResult(await reader.GetAsync(account.Id, ct), approvalId,
            approvalId is null
                ? $"Đã gửi email mời kích hoạt tới {email}"
                : $"Đã gửi email mời tới {email}. Vai trò có quyền tài chính chờ người thứ 2 duyệt.", []);
    }
}
