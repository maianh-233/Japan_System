using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Users.ResendInvite;

/// <summary>PQ-08.E7 — gửi lại email mời cho tài khoản chưa kích hoạt (link cũ vô hiệu ngay).</summary>
public sealed record ResendInviteCommand(Guid AccountId) : ICommand;

public sealed class ResendInviteHandler(ISystemDbContext db, InvitationMailer mailer, IEmployeeDirectory employees, IAuditTrail audit)
    : ICommandHandler<ResendInviteCommand>
{
    public async Task<Result> Handle(ResendInviteCommand cmd, CancellationToken ct)
    {
        var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.Id == new UserAccountId(cmd.AccountId), ct);
        if (account is null) return PermissionErrors.UserNotFound;
        if (account.Status != UserAccountStatus.Invited) return PermissionErrors.NotInvited;

        await mailer.SendAsync(account, ct);
        var name = (await employees.FindAsync(account.EmployeeId, ct))?.FullName ?? account.Email;
        audit.Record(AuditCategory.Permission, AuditSeverity.Info, "Gửi lại lời mời", name, account.Email);
        return Result.Success();
    }
}
