using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JPCenter.Api.Common.Realtime;

public static class RealtimeExtensions
{
    /// <summary>SignalR (1 instance, không backplane — ADR 0001 / infra-scope) + thay notifier mặc định bằng bản SignalR</summary>
    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSignalR(o => o.EnableDetailedErrors = false);
        services.Replace(ServiceDescriptor.Singleton<IRealtimeNotifier, SignalRRealtimeNotifier>());
        return services;
    }

    /// <summary>Host gọi 1 lần: hub <c>/hubs/realtime</c>, cần đăng nhập (mọi cổng)</summary>
    public static IEndpointRouteBuilder MapRealtimeHub(this IEndpointRouteBuilder app)
    {
        // Kết nối sống lâu → bỏ giới hạn 30 giây của request thường
        app.MapHub<RealtimeHub>(RealtimeHub.Path).RequireSignedIn().DisableRequestTimeout();
        return app;
    }
}
