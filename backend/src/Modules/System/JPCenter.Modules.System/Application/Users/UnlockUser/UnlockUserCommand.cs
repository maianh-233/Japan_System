using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Users.UnlockUser;

/// <summary>PQ-08 — mở khóa: về "đang hoạt động", xóa lý do, nhật ký mức warning.</summary>
public sealed record UnlockUserCommand(Guid AccountId) : ICommand<UserAccessDto>;

public sealed class UnlockUserHandler(ISystemDbContext db, ICurrentUser user, UserEditGuard guard, PermissionChanges changes,
    IEmployeeDirectory employees, UserReader reader, IAuditTrail audit) : ICommandHandler<UnlockUserCommand, UserAccessDto>
{
    public async Task<Result<UserAccessDto>> Handle(UnlockUserCommand cmd, CancellationToken ct)
    {
        var target = await guard.LoadTargetAsync(cmd.AccountId, user.Access!, ct);
        if (target.IsFailure) return target.Error;
        var account = target.Value;
        if (account.Status != UserAccountStatus.Locked) return PermissionErrors.NotLocked;

        var reason = account.LockedReason;
        account.Unlock();
        await db.SaveChangesAsync(ct);
        changes.Invalidate(account.Id.Value);

        var name = (await employees.FindAsync(account.EmployeeId, ct))?.FullName ?? account.Email;
        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Mở khóa tài khoản", name, $"Lý do khóa trước đó: {reason}");
        return await reader.GetAsync(account.Id, ct);
    }
}
