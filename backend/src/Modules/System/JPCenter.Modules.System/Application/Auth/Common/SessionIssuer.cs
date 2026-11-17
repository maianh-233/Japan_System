using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>
/// Tạo / làm mới phiên đăng nhập (AUTH-02): access token 15 phút + refresh token xoay vòng mỗi lần làm mới.
/// "Ghi nhớ" → refresh 30 ngày; không → 12 giờ và cookie mất khi đóng trình duyệt. Người gọi tự SaveChanges.
/// </summary>
public sealed class SessionIssuer(ISystemDbContext db, ITokenService tokens, IClock clock, IRequestInfo request)
{
    public static readonly TimeSpan BrowserSessionLifetime = TimeSpan.FromHours(12);

    public (UserSession Session, string AccessToken, DateTimeOffset AccessExpiresAt, IssuedRefreshToken Refresh) Start(
        ISignInAccount account, Portal portal, bool remember)
    {
        var now = clock.UtcNow;
        var refresh = tokens.NewSecret();
        var expires = remember ? now.AddDays(tokens.RememberDays) : now + BrowserSessionLifetime;
        var session = UserSession.Start(account.Kind, account.AccountGuid, tokens.Hash(refresh), request.Device, request.Ip, remember, now, expires);
        db.UserSessions.Add(session);

        var (access, accessExpires) = tokens.IssueAccessToken(account.AccountGuid, portal, account.TokenVersion, session.Id.Value);
        return (session, access, accessExpires, new IssuedRefreshToken(refresh, expires, remember));
    }

    /// <summary>Xoay vòng refresh token của phiên + phát access token mới.</summary>
    public (string AccessToken, DateTimeOffset AccessExpiresAt, IssuedRefreshToken Refresh)? Rotate(
        UserSession session, ISignInAccount account, Portal portal)
    {
        var now = clock.UtcNow;
        var refresh = tokens.NewSecret();
        var expires = session.Remember ? now.AddDays(tokens.RememberDays) : now + BrowserSessionLifetime;
        if (session.Rotate(tokens.Hash(refresh), request.Ip, now, expires).IsFailure) return null;

        var (access, accessExpires) = tokens.IssueAccessToken(account.AccountGuid, portal, account.TokenVersion, session.Id.Value);
        return (access, accessExpires, new IssuedRefreshToken(refresh, expires, session.Remember));
    }

    /// <summary>Phát lại access token cho phiên hiện tại (sau khi đổi mật khẩu — token_version đã tăng).</summary>
    public (string AccessToken, DateTimeOffset AccessExpiresAt) Reissue(ISignInAccount account, Portal portal, Guid sessionId) =>
        tokens.IssueAccessToken(account.AccountGuid, portal, account.TokenVersion, sessionId);
}
