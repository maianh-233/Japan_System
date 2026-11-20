using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Notifications.Api;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Infrastructure.Persistence;
using JPCenter.Modules.Notifications.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Notifications.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace JPCenter.Modules.Notifications;

/// <summary>
/// Điểm vào module Notifications (chuông thông báo, user story 10 TB-xx) — schema <c>notifications</c>, API <c>/api/v1/notifications</c>.
/// Module khác gửi thông báo bằng integration event <c>NotificationRequested</c> (Notifications.Contracts).
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

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            services.AddQuartz(q => q.ScheduleJob<NotificationsCleanupJob>(
                trigger => trigger.WithIdentity("notifications-cleanup")
                    .WithCronSchedule(NotificationsCleanupJob.Cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
                job => job.WithIdentity("notifications-cleanup")));
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => NotificationsEndpoints.Map(group);
}
