using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Infrastructure.Persistence;
using JPCenter.Modules.Notifications.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Notifications;

/// <summary>
/// Điểm vào module Notifications (chuông thông báo) — schema <c>notifications</c>, API <c>/api/v1/notifications</c>.
/// Giai đoạn 16 phase 2: lưu DB + dữ liệu mẫu; API, SignalR, sự kiện <c>NotificationRequested</c> ở phase 3.
/// </summary>
public sealed class NotificationsModule : IModule
{
    public string Name => NotificationsDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration, NotificationsDbContext.SchemaName);
        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddHandlersFromAssembly(typeof(NotificationsModule).Assembly);

        services.AddScoped<IDataSeeder, NotificationsSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) { }
}
