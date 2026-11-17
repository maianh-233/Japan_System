using JPCenter.Api.Common.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace JPCenter.Api.Common.Security;

public static class AuthenticationExtensions
{
    /// <summary>
    /// M7 — Kiểm tra access token: chữ ký HS256 (cố định thuật toán), issuer, audience, hạn (lệch giờ ≤ 30 giây).
    /// Mặc định MỌI endpoint cần đăng nhập; endpoint công khai phải ghi rõ <c>.AllowAnonymous()</c>.
    /// </summary>
    public static IServiceCollection AddJpAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey phải dài tối thiểu 32 ký tự (xem .env.example)");
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddSingleton<AccessTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = AccessTokenIssuer.SigningKey(jwt),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = JwtClaims.Subject,
            };
            o.Events = new JwtBearerEvents
            {
                // Hết hạn → mã riêng để frontend tự làm mới token (AUTH-02); còn lại → đăng nhập lại
                OnChallenge = async ctx =>
                {
                    ctx.HandleResponse();
                    var expired = ctx.AuthenticateFailure is SecurityTokenExpiredException;
                    await ProblemFactory.WriteAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized,
                        expired ? "auth.token_expired" : "auth.unauthenticated",
                        expired ? "Phiên làm việc đã hết hạn." : "Bạn chưa đăng nhập hoặc phiên đăng nhập không hợp lệ.");
                },
                OnForbidden = ctx => ProblemFactory.WriteAsync(ctx.HttpContext, StatusCodes.Status403Forbidden,
                    "system.permission.forbidden", "Bạn không có quyền thực hiện thao tác này."),
            };
        });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }
}
