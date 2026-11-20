using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic.Api;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Application.Evaluations.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Application.Rooms.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.Modules.Academic.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Academic.Infrastructure.Services;
using Quartz;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Academic;

/// <summary>Điểm vào module Academic (Đào tạo) — schema <c>academic</c>, API <c>/api/v1/academic</c>.</summary>
public sealed class AcademicModule : IModule
{
    public string Name => AcademicDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AcademicDbContext>(configuration, AcademicDbContext.SchemaName);
        services.AddScoped<IAcademicDbContext>(sp => sp.GetRequiredService<AcademicDbContext>());
        services.AddHandlersFromAssembly(typeof(AcademicModule).Assembly);

        // Dùng chung giữa các use case (user story 09)
        services.AddScoped<ClassUsage>();

        // Cho module khác (Academic.Contracts)
        services.AddScoped<JPCenter.Modules.Academic.Contracts.IClassLookup, JPCenter.Modules.Academic.Infrastructure.Services.ClassLookup>();
        services.AddScoped<CourseReader>();
        services.AddScoped<CourseRules>();
        services.AddScoped<CurriculumReader>();
        services.AddScoped<CurriculumRules>();
        services.AddScoped<CurriculumEditor>();

        // Dùng chung giữa các use case (user story 07 — lớp, buổi, phòng)
        services.AddScoped<AcademicAccess>();
        services.AddScoped<PeopleNames>();
        services.AddScoped<RoomReader>();
        services.AddScoped<RoomRules>();
        services.AddScoped<ClassRoster>();
        services.AddScoped<ClassReader>();
        services.AddScoped<ClassRules>();
        services.AddScoped<UpcomingSessions>();
        services.AddScoped<SessionScheduler>();
        services.AddScoped<SessionReader>();
        services.AddScoped<SessionConflicts>();
        services.AddScoped<SessionConfirmer>();

        // Cổng GV / HV / PH, đơn từ, điểm danh (giai đoạn 15)
        services.AddScoped<PortalOwner>();
        services.AddScoped<TeacherSessions>();
        services.AddScoped<MakeupSync>();
        services.AddScoped<RequestReader>();
        services.AddScoped<MakeupChecker>();
        services.AddScoped<LeaveAccess>();
        services.AddScoped<PromotionReader>();
        services.AddScoped<EvaluationReader>();
        services.AddScoped<IAttendanceStats, AttendanceStats>();

        services.AddScoped<IDataSeeder, AcademicSeeder>();
        services.AddScoped<IDataSeeder, ClassSeeder>();
        services.AddScoped<IDataSeeder, RequestSeeder>();

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            var vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            services.AddQuartz(q =>
            {
                q.ScheduleJob<SubstituteInviteJob>(
                    trigger => trigger.WithIdentity("academic-substitute-invites").WithCronSchedule(SubstituteInviteJob.Cron, x => x.InTimeZone(vietnam)),
                    job => job.WithIdentity("academic-substitute-invites"));
                q.ScheduleJob<AcademicDailyJob>(
                    trigger => trigger.WithIdentity("academic-daily").WithCronSchedule(AcademicDailyJob.Cron, x => x.InTimeZone(vietnam)),
                    job => job.WithIdentity("academic-daily"));
            });
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => AcademicEndpoints.Map(group);
}
