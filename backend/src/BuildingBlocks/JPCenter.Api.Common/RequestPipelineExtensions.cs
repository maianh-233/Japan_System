using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Middleware;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JPCenter.Api.Common;

/// <summary>
/// Đường đi của request (docs/request-pipeline.md). Thứ tự middleware trong <see cref="UseRequestPipeline"/> là CỐ ĐỊNH:
/// lớp rẻ + chặn nhiều đặt trước; lớp cần biết "ai gọi" đặt sau xác thực; lớp cần biết "endpoint nào" chạy sau định tuyến.
/// </summary>
public static class RequestPipelineExtensions
{
    public const string CorsPolicy = "frontend";

    /// <summary>1 MB cho JSON — endpoint nhập file nới riêng bằng <c>.WithBodyLimit()</c>.</summary>
    public const long DefaultBodyLimit = 1024 * 1024;

    public static IServiceCollection AddRequestPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());
        services.Replace(ServiceDescriptor.Scoped<IRequestInfo, HttpRequestInfo>());
        services.TryAddSingleton<IUserAccessCache, UserAccessCache>();
        services.TryAddSingleton<ISessionRevocationList, SessionRevocationList>();

        services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = DefaultBodyLimit);
        services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(30), TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        });

        var origins = (configuration["Cors:AllowedOrigins"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
        {
            if (origins.Length == 0) return;
            p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
                .WithExposedHeaders("Retry-After", CorrelationIdMiddleware.Header, Idempotency.IdempotencyExtensions.ReplayedHeader);
        }));

        services.AddJpAuthentication(configuration);
        services.AddJpRateLimiting(configuration);
        return services;
    }

    public static WebApplication UseRequestPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();                          // M1  IP thật sau proxy
        app.UseMiddleware<CorrelationIdMiddleware>();       // M2  mã truy vết
        app.UseExceptionHandler();                          // M3  lỗi → ProblemDetails
        app.UseStatusCodePages();
        app.UseMiddleware<SecurityHeadersMiddleware>();     // M4  header bảo mật
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseRouting();
        app.UseMiddleware<Validation.BodySizeMiddleware>(); //     body quá lớn → 413 trước khi đọc
        app.UseCors(CorsPolicy);                            // M5  chỉ domain frontend
        app.UseAuthentication();                            // M7  kiểm tra token
        app.UseMiddleware<CurrentUserMiddleware>();         // M8  nạp quyền, chặn token đã thu hồi
        app.UseMiddleware<PenaltyMiddleware>();             //     người đang bị phạt vì spam
        app.UseRateLimiter();                               // M6/M9 giới hạn tần suất (IP / người + endpoint)
        app.UseAuthorization();                             // M10 bắt buộc đăng nhập (quyền chi tiết: filter RequirePermission)
        app.UseRequestTimeouts();
        return app;
    }
}
