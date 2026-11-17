using JPCenter.Api.Common.Errors;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Api.Common.Security;

/// <summary>Quyền cần để gọi endpoint — metadata đọc được bởi test kiến trúc (mọi endpoint phải khai báo).</summary>
public sealed record RequiredPermission(string Page, PermissionAction Action)
{
    public string Code => CurrentUserExtensions.PermissionCode(Page, Action);
}

/// <summary>Endpoint chỉ cần đăng nhập, không cần mã quyền (trang Cá nhân — PQ-10.E5).</summary>
public sealed record SelfServiceEndpoint;

/// <summary>Endpoint chỉ dành cho các cổng này.</summary>
public sealed record RequiredPortals(IReadOnlyList<Portal> Portals);

public static class EndpointSecurityExtensions
{
    /// <summary>M10 — Kiểm tra mã quyền <c>page.action</c>. Thiếu → 403 <c>system.permission.forbidden</c> + nhật ký cảnh báo.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string page, PermissionAction action)
        where TBuilder : IEndpointConventionBuilder
    {
        var required = new RequiredPermission(page, action);
        builder.RequireAuthorization();
        builder.WithMetadata(required);
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var user = http.RequestServices.GetRequiredService<ICurrentUser>();
            if (user.Can(required.Page, required.Action)) return await next(ctx);

            http.RequestServices.GetRequiredService<IAuditTrail>().Record(AuditCategory.Permission, AuditSeverity.Warning,
                "Từ chối truy cập", $"{http.Request.Method} {http.Request.Path}", $"Thiếu quyền {required.Code}");
            return new Results.ErrorHttpResult(Error.Forbidden("system.permission.forbidden", "Bạn không có quyền thực hiện thao tác này."));
        });
        return builder;
    }

    /// <summary>Chỉ cần đăng nhập (trang Cá nhân: hồ sơ, đổi mật khẩu, thiết bị của tôi...).</summary>
    public static TBuilder RequireSignedIn<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.WithMetadata(new SelfServiceEndpoint());
        return builder;
    }

    /// <summary>Chỉ cho các cổng chỉ định (vd endpoint cổng PH). Cổng khác → 403.</summary>
    public static TBuilder RequirePortal<TBuilder>(this TBuilder builder, params Portal[] portals) where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.WithMetadata(new RequiredPortals(portals));
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var user = ctx.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
            if (user.Access is { } a && portals.Contains(a.Portal)) return await next(ctx);
            return new Results.ErrorHttpResult(Error.Forbidden("system.permission.wrong_portal", "Chức năng này không dành cho tài khoản của bạn."));
        });
        return builder;
    }
}
