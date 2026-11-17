using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Auth.AcceptInvitation;
using JPCenter.Modules.System.Application.Auth.ChangePassword;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Auth.ForgotPassword;
using JPCenter.Modules.System.Application.Auth.GetInvitation;
using JPCenter.Modules.System.Application.Auth.GetMe;
using JPCenter.Modules.System.Application.Auth.Login;
using JPCenter.Modules.System.Application.Auth.Logout;
using JPCenter.Modules.System.Application.Auth.RefreshSession;
using JPCenter.Modules.System.Application.Auth.ResetPassword;
using JPCenter.Modules.System.Application.Auth.VerifyResetOtp;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>
/// Đăng nhập & bảo mật — user story 01. Đường dẫn: <c>/api/v1/system/auth/*</c>.
/// Refresh token nằm trong cookie HttpOnly (JavaScript không đọc được), access token trả trong body.
/// </summary>
public static class AuthEndpoints
{
    public const string RefreshCookie = "jp_refresh";
    public const string CookiePath = "/api/v1/system/auth";

    public static void Map(IEndpointRouteBuilder system)
    {
        var auth = system.MapGroup("/auth");

        // AUTH-01
        auth.MapPost("/login", async (LoginCommand cmd, ISender sender, HttpContext http, CancellationToken ct) =>
                SignIn(http, await sender.Send(cmd, ct)))
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);

        // AUTH-02 — đổi refresh token (cookie) lấy access token mới
        auth.MapPost("/refresh", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var result = await sender.Send(new RefreshSessionCommand(http.Request.Cookies[RefreshCookie]), ct);
                if (result.IsFailure) ClearCookie(http);
                return SignIn(http, result);
            })
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);

        // AUTH-03
        auth.MapPost("/logout", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var result = await sender.Send(new LogoutCommand(http.Request.Cookies[RefreshCookie]), ct);
                ClearCookie(http);
                return result.ToHttpResult();
            })
            .AllowAnonymous();

        auth.MapGet("/me", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMeQuery(), ct)).ToHttpResult())
            .RequireSignedIn();

        // AUTH-04 — quên mật khẩu: gửi OTP → xác thực OTP → đặt mật khẩu mới
        auth.MapPost("/password/forgot", async (ForgotPasswordCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToHttpResult())
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);
        auth.MapPost("/password/verify-otp", async (VerifyResetOtpCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToHttpResult())
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);
        auth.MapPost("/password/reset", async (ResetPasswordCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToHttpResult())
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);

        // AUTH-05 — kích hoạt tài khoản được mời
        auth.MapGet("/invitations/{token}", async (string token, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetInvitationQuery(token), ct)).ToHttpResult())
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);
        auth.MapPost("/invitations/accept", async (AcceptInvitationCommand cmd, ISender sender, HttpContext http, CancellationToken ct) =>
                SignIn(http, await sender.Send(cmd, ct)))
            .AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);

        // AUTH-06
        auth.MapPost("/password/change", async (ChangePasswordCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToHttpResult())
            .RequireSignedIn().RequireRateLimiting(RateLimitPolicies.WriteSensitive);
    }

    /// <summary>Thành công → đặt cookie refresh + trả { accessToken, accessTokenExpiresAt, user }.</summary>
    private static IResult SignIn(HttpContext http, Result<SignInResult> result)
    {
        if (result.IsFailure) return result.Error.ToProblem();
        var r = result.Value;
        http.Response.Cookies.Append(RefreshCookie, r.Refresh.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            // Không "Ghi nhớ" → cookie phiên: đóng trình duyệt là mất (AUTH-02 bước 1)
            Expires = r.Refresh.Persistent ? r.Refresh.ExpiresAt : null,
        });
        return Microsoft.AspNetCore.Http.Results.Ok(new AuthResponse(r.AccessToken, r.AccessTokenExpiresAt, r.User));
    }

    private static void ClearCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = CookiePath, HttpOnly = true, SameSite = SameSiteMode.Strict });

    /// <summary>Phản hồi đăng nhập — KHÔNG chứa refresh token (nằm trong cookie).</summary>
    public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, SessionUserDto User);
}
