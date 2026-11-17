using System.Globalization;
using System.Threading.RateLimiting;
using JPCenter.Api.Common.Errors;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Auditing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Api.Common.RateLimiting;

/// <summary>
/// M6 / M9 — Giới hạn tần suất bằng <c>Microsoft.AspNetCore.RateLimiting</c> có sẵn. Request bị chặn trả 429 ngay ở middleware,
/// không chạm handler / database. Bộ đếm trong bộ nhớ (1 instance).
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddJpRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var o = configuration.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Section));
        services.AddSingleton<PenaltyBox>();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = OnRejected;
            if (!o.Enabled) return;

            // Mọi request: lưới an toàn theo người · Ghi (POST/PUT/PATCH/DELETE): theo người + endpoint, cho phép "nhanh từng đợt"
            limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetSlidingWindowLimiter(
                    PartitionKey(http), _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = o.GlobalPerMinute, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0,
                    })),
                PartitionedRateLimiter.Create<HttpContext, string>(http => IsWrite(http)
                    ? RateLimitPartition.GetTokenBucketLimiter($"{PartitionKey(http)}|{RouteKey(http)}", _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = o.WriteBurst, TokensPerPeriod = o.WriteRefillPerSecond, ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        AutoReplenishment = true, QueueLimit = 0,
                    })
                    : RateLimitPartition.GetNoLimiter("read")));

            limiter.AddPolicy(RateLimitPolicies.Anonymous, http => RateLimitPartition.GetFixedWindowLimiter(
                $"ip:{Ip(http)}|{RouteKey(http)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = o.AnonymousPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                }));

            limiter.AddPolicy(RateLimitPolicies.WriteSensitive, http => RateLimitPartition.GetFixedWindowLimiter(
                $"{PartitionKey(http)}|{RouteKey(http)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = o.SensitivePer10Seconds, Window = TimeSpan.FromSeconds(10), QueueLimit = 0,
                }));

            limiter.AddPolicy(RateLimitPolicies.Export, http => RateLimitPartition.GetConcurrencyLimiter(
                $"export:{PartitionKey(http)}", _ => new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 }));

            limiter.AddPolicy(RateLimitPolicies.Import, http => RateLimitPartition.GetConcurrencyLimiter(
                $"import:{PartitionKey(http)}", _ => new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 }));
        });
        return services;
    }

    /// <summary>Người dùng đã đăng nhập → theo tài khoản (nhiều người chung wifi không ảnh hưởng nhau); chưa đăng nhập → theo IP.</summary>
    public static string PartitionKey(HttpContext http) =>
        http.User.FindFirst(JwtClaims.Subject)?.Value is { } sub ? $"user:{sub}" : $"ip:{Ip(http)}";

    internal static bool IsWrite(HttpContext http) =>
        !(HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method));

    private static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Theo mẫu đường dẫn (vd <c>/api/v1/crm/leads/{id}</c>) — id khác nhau vẫn chung 1 bộ đếm.</summary>
    private static string RouteKey(HttpContext http) =>
        (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? "/";

    private static async ValueTask OnRejected(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) ? (int)Math.Ceiling(ra.TotalSeconds) : 10;
        retryAfter = Math.Max(1, retryAfter);
        http.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

        var penalty = http.RequestServices.GetRequiredService<PenaltyBox>();
        if (penalty.RecordViolation(PartitionKey(http)))
        {
            http.RequestServices.GetRequiredService<IAuditTrail>().Record(AuditCategory.Security, AuditSeverity.Warning,
                "Tạm khóa thao tác ghi do gửi yêu cầu liên tục", $"{http.Request.Method} {http.Request.Path}",
                $"Vượt giới hạn nhiều lần — khóa {PartitionKey(http)}");
        }

        await ProblemFactory.WriteAsync(http, StatusCodes.Status429TooManyRequests, "system.too_many_requests",
            $"Bạn thao tác quá nhanh. Vui lòng đợi {retryAfter} giây rồi thử lại, không bấm liên tục.",
            new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    }
}
