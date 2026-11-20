using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Students.Api;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using JPCenter.Modules.Students.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Students.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace JPCenter.Modules.Students;

/// <summary>Điểm vào module Students (Học viên) — schema <c>students</c>, API <c>/api/v1/students</c>.</summary>
public sealed class StudentsModule : IModule
{
    public string Name => StudentsDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudentsDbContext>(configuration, StudentsDbContext.SchemaName);
        services.AddScoped<IStudentsDbContext>(sp => sp.GetRequiredService<StudentsDbContext>());
        services.AddHandlersFromAssembly(typeof(StudentsModule).Assembly);

        // Cho module khác (Students.Contracts)
        services.AddScoped<IEnrollmentLookup, EnrollmentLookup>();

        // Dùng chung giữa các use case (user story 06)
        services.AddScoped<StudentAccess>();
        services.AddScoped<EmployeeNames>();
        services.AddScoped<StudentReader>();
        services.AddScoped<StudentCodes>();
        services.AddScoped<EnrollmentReader>();
        services.AddScoped<EnrollmentRules>();
        services.AddScoped<RiskCalculator>();
        services.AddScoped<CareCaseReader>();

        services.AddScoped<IDataSeeder, StudentSeeder>();
        services.AddScoped<IDataSeeder, CareSeeder>();

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            services.AddQuartz(q => q.ScheduleJob<RiskAssessmentJob>(
                trigger => trigger.WithIdentity("students-risk")
                    .WithCronSchedule(RiskAssessmentJob.Cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
                job => job.WithIdentity("students-risk")));
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => StudentsEndpoints.Map(group);
}
