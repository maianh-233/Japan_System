using System.Globalization;
using JPCenter.Api.Common.Errors;
using JPCenter.Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace JPCenter.Api.Common.Security;

/// <summary>
/// M8 — Sau khi token hợp lệ: nạp quyền của tài khoản (cache), kiểm tra token chưa bị thu hồi.
/// Token hợp lệ về chữ ký nhưng tài khoản đã khóa / đổi mật khẩu / đổi quyền (lệch <c>tv</c>) → 401 ngay.
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next, IUserAccessCache cache, ISessionRevocationList revoked)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var principal = http.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            await next(http);
            return;
        }

        var sub = principal.FindFirst(JwtClaims.Subject)?.Value;
        var portalClaim = principal.FindFirst(JwtClaims.Portal)?.Value;
        var tv = principal.FindFirst(JwtClaims.TokenVersion)?.Value;
        if (!Guid.TryParse(sub, out var accountId)
            || !Enum.TryParse<Portal>(portalClaim, ignoreCase: true, out var portal)
            || !int.TryParse(tv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokenVersion))
        {
            await Reject(http, "auth.token_invalid", "Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại.");
            return;
        }

        var access = await cache.GetAsync(accountId, portal is Portal.Student or Portal.Parent, http.RequestAborted);
        if (access is null || access.Portal != portal || access.TokenVersion != tokenVersion)
        {
            await Reject(http, "auth.token_revoked", "Phiên đăng nhập đã hết hiệu lực, vui lòng đăng nhập lại.");
            return;
        }
        if (!access.IsActive)
        {
            await Reject(http, "auth.account_locked", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
            return;
        }

        if (Guid.TryParse(principal.FindFirst(JwtClaims.SessionId)?.Value, out var sid))
        {
            // Thiết bị này vừa đăng xuất / bị đăng xuất từ xa → token còn hạn cũng không dùng được nữa
            if (revoked.IsRevoked(sid))
            {
                await Reject(http, "auth.session_revoked", "Phiên đăng nhập trên thiết bị này đã bị đăng xuất.");
                return;
            }
            http.Items[HttpCurrentUser.SessionKey] = sid;
        }

        http.Items[HttpCurrentUser.AccessKey] = access;
        await next(http);
    }

    private static Task Reject(HttpContext http, string code, string title) =>
        ProblemFactory.WriteAsync(http, StatusCodes.Status401Unauthorized, code, title);
}
