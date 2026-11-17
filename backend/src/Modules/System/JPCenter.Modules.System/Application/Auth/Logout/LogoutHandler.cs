using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.Logout;

/// <summary>AUTH-03. Luôn trả thành công (AUTH-03.E1: không tìm thấy phiên cũng coi như đã đăng xuất).</summary>
public sealed class LogoutHandler(ISystemDbContext db, ICurrentUser user, ITokenService tokens, ISessionRevocationList revoked,
    IAuditTrail audit, IClock clock) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        UserSession? session = null;
        if (user.SessionId is { } sid)
            session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == new UserSessionId(sid), ct);
        if (session is null && !string.IsNullOrWhiteSpace(cmd.RefreshToken))
        {
            var hash = tokens.Hash(cmd.RefreshToken);
            session = await db.UserSessions.FirstOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
        }
        if (session is null || session.RevokedAt is not null) return Result.Success();

        session.Revoke("logout", clock.UtcNow);
        await db.SaveChangesAsync(ct);
        revoked.Revoke(session.Id.Value);

        audit.Record(AuditCategory.Auth, AuditSeverity.Info, "Đăng xuất", session.Device,
            actor: user.Access is null ? new AuditActor(session.AccountId, "—", "—") : null);
        return Result.Success();
    }
}
