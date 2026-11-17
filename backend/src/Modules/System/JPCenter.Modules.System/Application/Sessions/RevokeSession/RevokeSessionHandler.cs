using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Sessions.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Sessions.RevokeSession;

/// <summary>Chỉ thu hồi được phiên CỦA MÌNH — phiên người khác trả "không tìm thấy" (không lộ tồn tại).</summary>
public sealed class RevokeSessionHandler(ISystemDbContext db, ICurrentUser user, ISessionRevocationList revoked, IAuditTrail audit, IClock clock)
    : ICommandHandler<RevokeSessionCommand>
{
    public async Task<Result> Handle(RevokeSessionCommand cmd, CancellationToken ct)
    {
        if (MyAccount.Of(user) is not var (kind, id)) return AuthErrors.SessionNotFound;
        var session = await db.UserSessions.FirstOrDefaultAsync(
            x => x.Id == new UserSessionId(cmd.SessionId) && x.AccountKind == kind && x.AccountId == id, ct);
        if (session is null || session.RevokedAt is not null) return AuthErrors.SessionNotFound;

        var isCurrent = cmd.SessionId == user.SessionId;
        session.Revoke(isCurrent ? "logout" : "logout_others", clock.UtcNow);
        await db.SaveChangesAsync(ct);
        revoked.Revoke(session.Id.Value);

        audit.Record(AuditCategory.Auth, AuditSeverity.Info, isCurrent ? "Đăng xuất" : "Đăng xuất thiết bị khác", session.Device);
        return Result.Success();
    }
}
