using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Api;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.Hr.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Hr.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace JPCenter.Modules.Hr;

/// <summary>Điểm vào module Hr (Nhân sự) — schema <c>hr</c>, API <c>/api/v1/hr</c>.</summary>
public sealed class HrModule : IModule
{
    public string Name => HrDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<HrDbContext>(configuration, HrDbContext.SchemaName);
        services.AddScoped<IHrDbContext>(sp => sp.GetRequiredService<HrDbContext>());
        services.AddHandlersFromAssembly(typeof(HrModule).Assembly);

        // Cho module khác (Hr.Contracts)
        services.AddScoped<IEmployeeDirectory, EmployeeDirectory>();

        // Dùng chung giữa các use case
        services.AddScoped<EmployeeAccess>();
        services.AddScoped<EmployeeRules>();

        services.AddScoped<IDataSeeder, HrSeeder>();

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            services.AddQuartz(q => q.ScheduleJob<HrDailyJob>(
                trigger => trigger.WithIdentity("hr-daily")
                    .WithCronSchedule(HrDailyJob.Cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
                job => job.WithIdentity("hr-daily")));
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => HrEndpoints.Map(group);
}
