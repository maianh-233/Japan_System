using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.Work.Api;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Handovers.Common;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Application.Shifts.MyShifts;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Contracts;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using JPCenter.Modules.Work.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Work.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace JPCenter.Modules.Work;

/// <summary>Điểm vào module Work (Công việc) — schema <c>work</c>, API <c>/api/v1/work</c>: task, xếp ca, bàn giao ca (user story 14).</summary>
public sealed class WorkModule : IModule
{
    public string Name => WorkDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<WorkDbContext>(configuration, WorkDbContext.SchemaName);
        services.AddScoped<IWorkDbContext>(sp => sp.GetRequiredService<WorkDbContext>());
        services.AddHandlersFromAssembly(typeof(WorkModule).Assembly);

        // Cho module khác (Work.Contracts) + Thùng rác
        services.AddScoped<IShiftSchedule, ShiftSchedule>();
        services.AddScoped<ITrashHandler, TaskTrashHandler>();

        // Dùng chung giữa các use case
        services.AddScoped<WorkPeople>();
        services.AddScoped<WorkNotices>();
        services.AddScoped<TaskDesk>();
        services.AddScoped<ShiftDesk>();
        services.AddScoped<MyShiftReader>();
        services.AddScoped<HandoverDesk>();

        services.AddScoped<IDataSeeder, WorkSeeder>();

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            services.AddQuartz(q => q.ScheduleJob<WorkDailyJob>(
                trigger => trigger.WithIdentity("work-daily")
                    .WithCronSchedule(WorkDailyJob.Cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
                job => job.WithIdentity("work-daily")));
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => WorkEndpoints.Map(group);
}
