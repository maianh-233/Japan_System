using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.PortalAccounts.Common;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.PortalAccounts.SetPortalAccountLock;

public sealed class SetPortalAccountLockHandler(ISystemDbContext db, PortalOwnerAccess owners, SessionRevoker revoker, IUserAccessCache accessCache, IAuditTrail audit)
    : ICommandHandler<SetPortalAccountLockCommand, PortalAccountDto>
{
    public async Task<Result<PortalAccountDto>> Handle(SetPortalAccountLockCommand cmd, CancellationToken ct)
    {
        var account = await db.PortalAccounts.FirstOrDefaultAsync(x => x.Id == new PortalAccountId(cmd.AccountId), ct);
        if (account is null || await owners.FindAsync(account.OwnerType, account.OwnerId, PermissionAction.Update, ct) is null)
            return PortalAccountErrors.NotFound;

        if (cmd.Locked)
        {
            account.Lock();
            await revoker.RevokeAllAsync(AccountKind.Portal, account.Id.Value, "account_locked", null, ct);
        }
        else
        {
            account.Activate();
        }
        await db.SaveChangesAsync(ct);
        accessCache.Invalidate(account.Id.Value);

        audit.Record(AuditCategory.Data, cmd.Locked ? AuditSeverity.Warning : AuditSeverity.Info,
            cmd.Locked ? "Khóa tài khoản cổng" : "Mở khóa tài khoản cổng", $"{account.DisplayName} ({account.Username})");
        return account.ToDto();
    }
}
