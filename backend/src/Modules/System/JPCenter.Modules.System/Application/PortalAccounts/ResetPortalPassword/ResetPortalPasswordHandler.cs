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

namespace JPCenter.Modules.System.Application.PortalAccounts.ResetPortalPassword;

public sealed class ResetPortalPasswordHandler(
    ISystemDbContext db, PortalCredentialSender credentials, SessionRevoker revoker, IUserAccessCache accessCache, IAuditTrail audit)
    : ICommandHandler<ResetPortalPasswordCommand, PortalCredentialDto>
{
    public async Task<Result<PortalCredentialDto>> Handle(ResetPortalPasswordCommand cmd, CancellationToken ct)
    {
        var account = await db.PortalAccounts.FirstOrDefaultAsync(x => x.Id == new PortalAccountId(cmd.AccountId), ct);
        if (account is null) return PortalAccountErrors.NotFound;

        await revoker.RevokeAllAsync(AccountKind.Portal, account.Id.Value, "admin", null, ct);
        var result = await credentials.IssueAsync(account, ct, () => db.SaveChangesAsync(ct));
        accessCache.Invalidate(account.Id.Value);

        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Đặt lại mật khẩu tài khoản cổng", $"{account.DisplayName} ({account.Username})");
        return result;
    }
}
