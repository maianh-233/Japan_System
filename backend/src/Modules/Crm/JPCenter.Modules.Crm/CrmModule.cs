using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Api;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Guardians.Common;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Modules.Crm.Application.Appointments.Common;
using JPCenter.Modules.Crm.Application.Campaigns.Common;
using JPCenter.Modules.Crm.Application.LeadRules.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Application.Messages.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.Public;
using Quartz;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using JPCenter.Modules.Crm.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Crm.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Crm;

/// <summary>
/// Điểm vào module Crm (Tuyển sinh, user story 05) — schema <c>crm</c>, API <c>/api/v1/crm</c>.
/// Job mỗi phút: quét SLA lead, nhắc lịch hẹn (5 phút), hết hạn link test (đầu giờ).
/// </summary>
public sealed class CrmModule : IModule
{
    public string Name => CrmDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.SchemaName);
        services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        services.AddHandlersFromAssembly(typeof(CrmModule).Assembly);

        // Dùng chung giữa các use case
        services.AddScoped<CrmAccess>();
        services.AddScoped<CrmNotices>();
        services.AddScoped<PeopleNames>();
        services.AddScoped<SalesTeam>();
        services.AddScoped<LeadRouting>();
        services.AddScoped<LeadReader>();
        services.AddScoped<LeadChecks>();
        services.AddScoped<LeadCodes>();
        services.AddScoped<LeadEditor>();
        services.AddScoped<GuardianLinks>();
        services.AddScoped<GuardianAccess>();
        services.AddScoped<LeadRuleReader>();
        services.AddScoped<PlacementBank>();
        services.AddScoped<PlacementReader>();
        services.AddScoped<PublicAttempts>();
        services.AddScoped<AppointmentDesk>();
        services.AddScoped<CampaignReader>();
        services.AddScoped<TemplateReader>();

        // Cho module khác (Crm.Contracts)
        services.AddScoped<IGuardianDirectory, GuardianDirectory>();
        services.AddScoped<ILeadOwnership, LeadOwnership>();

        services.AddScoped<IDataSeeder, CrmSeeder>();

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            var vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            services.AddQuartz(q => q.ScheduleJob<CrmMinuteJob>(
                trigger => trigger.WithIdentity("crm-minute").WithCronSchedule(CrmMinuteJob.Cron, x => x.InTimeZone(vietnam)),
                job => job.WithIdentity("crm-minute")));
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => CrmEndpoints.Map(group);
}
