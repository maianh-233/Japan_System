using JPCenter.Application.Common.Auditing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Api.Common.Auditing;

/// <summary>Endpoint có ghi nhật ký — metadata cho tài liệu / test.</summary>
public sealed record AuditedEndpoint(string Action, AuditCategory Category, AuditSeverity Severity);

public static class AuditEndpointExtensions
{
    /// <summary>
    /// F3 — Ghi 1 dòng nhật ký khi endpoint chạy THÀNH CÔNG (status &lt; 400), kèm các thay đổi trước → sau mà EF ghi nhận.
    /// Thất bại (sai dữ liệu, thiếu quyền...) không ghi ở đây — handler tự ghi nếu cần (vd đăng nhập sai).
    /// </summary>
    /// <param name="target">Đối tượng bị tác động; mặc định = id trên đường dẫn hoặc chính đường dẫn.</param>
    public static TBuilder Audited<TBuilder>(this TBuilder builder, string action, AuditCategory category,
        AuditSeverity severity = AuditSeverity.Info, Func<HttpContext, object?, string>? target = null)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AuditedEndpoint(action, category, severity));
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var result = await next(ctx);
            var status = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
            if (status >= 400) return result;

            var http = ctx.HttpContext;
            var value = (result as IValueHttpResult)?.Value;
            var what = target?.Invoke(http, value)
                ?? (http.Request.RouteValues.TryGetValue("id", out var id) ? id?.ToString() : null)
                ?? http.Request.Path.Value ?? "";
            http.RequestServices.GetRequiredService<IAuditTrail>().Record(category, severity, action, what);
            return result;
        });
        return builder;
    }
}
