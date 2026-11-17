using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Users.LockUser;

/// <summary>PQ-08 — khóa tài khoản: thu hồi mọi phiên ngay, nhật ký mức critical.</summary>
public sealed record LockUserCommand(Guid AccountId, string Reason) : ICommand<UserAccessDto>;

public sealed class LockUserValidator : AbstractValidator<LockUserCommand>
{
    public LockUserValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập lý do khóa").MaximumLength(500);
}

/// <summary>Không tự khóa mình (E4) · không khóa Super Admin cuối cùng (E5).</summary>
public sealed class LockUserHandler(ISystemDbContext db, ICurrentUser user, UserEditGuard guard, SuperAdminGuard superAdmins,
    PermissionChanges changes, SessionRevoker sessions, IEmployeeDirectory employees, UserReader reader, IAuditTrail audit)
    : ICommandHandler<LockUserCommand, UserAccessDto>
{
    public async Task<Result<UserAccessDto>> Handle(LockUserCommand cmd, CancellationToken ct)
    {
        var target = await guard.LoadTargetAsync(cmd.AccountId, user.Access!, ct);
        if (target.IsFailure) return target.Error;
        var account = target.Value;

        var roles = await changes.RolesAsync(ct);
        if (SuperAdminGuard.HasSuperRole(account.Roles.Select(r => r.RoleId), roles) && await superAdmins.WouldLeaveNoneAsync(account.Id, ct))
            return PermissionErrors.LastSuperAdmin;

        var reason = cmd.Reason.Trim();
        if (account.Lock(reason) is { IsFailure: true } failed) return failed.Error;
        var revoked = await sessions.RevokeAllAsync(AccountKind.Staff, account.Id.Value, "account_locked", null, ct);
        await db.SaveChangesAsync(ct);
        changes.Invalidate(account.Id.Value);

        // PQ-08.E8 (GV đang có lớp → cảnh báo) làm khi có module Academic
        var name = (await employees.FindAsync(account.EmployeeId, ct))?.FullName ?? account.Email;
        audit.Record(AuditCategory.Permission, AuditSeverity.Critical, "Khóa tài khoản", name, $"{reason} · thu hồi {revoked} phiên");
        return await reader.GetAsync(account.Id, ct);
    }
}
