using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.RefreshSession;

/// <summary>
/// AUTH-02. Refresh token cũ (đã xoay vòng) bị dùng lại → nghi bị đánh cắp → thu hồi MỌI phiên của tài khoản (AUTH-02.E2).
/// Ngoại lệ: dùng lại trong 30 giây sau khi xoay (2 tab làm mới cùng lúc) → chỉ báo thử lại.
/// </summary>
public sealed class RefreshSessionHandler(
    ISystemDbContext db, SignInAccounts accounts, ITokenService tokens, SessionIssuer sessions, IUserAccessProvider accessProvider,
    IUserAccessCache accessCache, ISessionRevocationList revoked, IAuditTrail audit, IClock clock)
    : ICommandHandler<RefreshSessionCommand, SignInResult>
{
    private static readonly TimeSpan ConcurrentRefreshGrace = TimeSpan.FromSeconds(30);

    public async Task<Result<SignInResult>> Handle(RefreshSessionCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshToken)) return AuthErrors.RefreshInvalid;
        var now = clock.UtcNow;
        var hash = tokens.Hash(cmd.RefreshToken);

        var session = await db.UserSessions.FirstOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
        if (session is null) return await HandleUnknownTokenAsync(hash, now, ct);
        if (!session.IsActive(now)) return AuthErrors.RefreshInvalid;

        var account = await accounts.FindAsync(session.AccountKind, session.AccountId, ct);
        if (account is null) return AuthErrors.RefreshInvalid;

        // AUTH-02.E3: bị khóa khi đang đăng nhập → lần làm mới kế tiếp bị từ chối
        if (account.CheckStatus() is { IsFailure: true } status)
        {
            session.Revoke("account_locked", now);
            await db.SaveChangesAsync(ct);
            return status.Error;
        }

        var access = await accessProvider.LoadAsync(account.AccountGuid, account.Kind == AccountKind.Portal, ct);
        if (access is null || !access.IsActive)
        {
            session.Revoke("account_locked", now);
            await db.SaveChangesAsync(ct);
            return UserAccountErrors.AccountLocked;
        }

        var rotated = sessions.Rotate(session, account, access.Portal);
        if (rotated is null) return AuthErrors.RefreshInvalid;
        await db.SaveChangesAsync(ct);

        var (accessToken, accessExpires, refresh) = rotated.Value;
        return new SignInResult(SessionUserFactory.Create(access, account.Email ?? account.LoginName, account.MustChangePassword),
            accessToken, accessExpires, refresh);
    }

    private async Task<Result<SignInResult>> HandleUnknownTokenAsync(string hash, DateTimeOffset now, CancellationToken ct)
    {
        var rotatedFrom = await db.UserSessions.FirstOrDefaultAsync(x => x.PreviousTokenHash == hash, ct);
        if (rotatedFrom is null) return AuthErrors.RefreshInvalid;
        if (rotatedFrom.IsActive(now) && now - rotatedFrom.LastSeenAt < ConcurrentRefreshGrace) return AuthErrors.RefreshConflict;

        // Token cũ bị dùng lại sau khi đã xoay vòng → coi như bị đánh cắp: đăng xuất mọi thiết bị
        var all = await db.UserSessions
            .Where(x => x.AccountKind == rotatedFrom.AccountKind && x.AccountId == rotatedFrom.AccountId && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var s in all)
        {
            s.Revoke("token_reuse", now);
            revoked.Revoke(s.Id.Value);
        }

        var account = await accounts.FindAsync(rotatedFrom.AccountKind, rotatedFrom.AccountId, ct);
        account?.RevokeTokens();
        await db.SaveChangesAsync(ct);
        accessCache.Invalidate(rotatedFrom.AccountId);

        audit.Record(AuditCategory.Security, AuditSeverity.Critical, "Refresh token bị dùng lại", account?.LoginName ?? rotatedFrom.AccountId.ToString(),
            $"Nghi bị đánh cắp phiên — đã đăng xuất {all.Count} thiết bị", new AuditActor(rotatedFrom.AccountId, account?.LoginName ?? "không xác định", "—"));
        return AuthErrors.TokenReused;
    }
}
