using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Sessions.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Sessions.RevokeOtherSessions;

public sealed class RevokeOtherSessionsHandler(ISystemDbContext db, ICurrentUser user, SessionRevoker revoker, IAuditTrail audit)
    : ICommandHandler<RevokeOtherSessionsCommand, int>
{
    public async Task<Result<int>> Handle(RevokeOtherSessionsCommand cmd, CancellationToken ct)
    {
        if (MyAccount.Of(user) is not var (kind, id)) return AuthErrors.SessionNotFound;
        var count = await revoker.RevokeAllAsync(kind, id, "logout_others", user.SessionId, ct);
        await db.SaveChangesAsync(ct);
        if (count > 0) audit.Record(AuditCategory.Auth, AuditSeverity.Info, "Đăng xuất thiết bị khác", $"{count} thiết bị");
        return count;
    }
}
