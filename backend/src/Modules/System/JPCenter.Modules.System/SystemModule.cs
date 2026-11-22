using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Idempotency;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Api;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.AuditLogs.Common;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Application.Trash.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.PortalAccounts.Common;
using JPCenter.Modules.System.Application.Roles.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.Modules.System.Infrastructure.Persistence.Seed;
using JPCenter.Modules.System.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace JPCenter.Modules.System;

/// <summary>
/// Điểm vào module System — schema <c>system</c>, API <c>/api/v1/system</c>.
/// Cung cấp cho middleware chung: nạp quyền (<see cref="IUserAccessProvider"/>), lưu nhật ký (<see cref="IAuditSink"/>),
/// lưu Idempotency-Key trong PostgreSQL (<see cref="IIdempotencyStore"/>) — thay các bản mặc định trong bộ nhớ.
/// </summary>
public sealed class SystemModule : IModule
{
    public string Name => SystemDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SystemDbContext>(configuration, SystemDbContext.SchemaName);
        services.AddScoped<ISystemDbContext>(sp => sp.GetRequiredService<SystemDbContext>());
        services.AddHandlersFromAssembly(typeof(SystemModule).Assembly);

        // Hạ tầng cho middleware chung
        services.Replace(ServiceDescriptor.Scoped<IUserAccessProvider, UserAccessProvider>());
        services.Replace(ServiceDescriptor.Scoped<IAuditSink, AuditLogSink>());
        services.Replace(ServiceDescriptor.Scoped<IIdempotencyStore, PostgresIdempotencyStore>());

        // Hạ tầng riêng của module
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IFrontendLinks, FrontendLinks>();
        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();

        // Cho module khác (System.Contracts)
        services.AddScoped<IBranchDirectory, BranchDirectory>();
        services.AddScoped<IStaffAccessDirectory, StaffAccessDirectory>();
        services.AddScoped<IHolidayCalendar, HolidayCalendar>();
        services.AddScoped<IApprovalInbox, ApprovalInbox>();
        services.AddScoped<ICenterSettings, CenterSettingsProvider>();

        // Thùng rác: mỗi loại dữ liệu xóa mềm 1 ITrashHandler (module khác tự đăng ký của mình)
        services.AddScoped<ITrashHandler, RoleTrashHandler>();

        // Dịch vụ dùng chung giữa các use case (Application/*/Common)
        services.AddScoped<SignInAccounts>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<SessionRevoker>();
        services.AddScoped<InvitationMailer>();
        services.AddScoped<InvitationLookup>();
        services.AddScoped<PortalCredentialSender>();
        services.AddScoped<PortalOwnerAccess>();
        services.AddScoped<PermissionChanges>();
        services.AddScoped<SuperAdminGuard>();
        services.AddScoped<RoleReader>();
        services.AddScoped<UserReader>();
        services.AddScoped<UserEditGuard>();
        services.AddScoped<ApprovalFactory>();
        services.AddScoped<ApprovalReader>();
        services.AddScoped<FinancePermissionApplier>();
        services.AddScoped<BranchReader>();
        services.AddScoped<BranchRules>();
        services.AddScoped<BranchStatusEvents>();
        services.AddSingleton<SettingsCache>();
        services.AddScoped<SettingsStore>();
        services.AddScoped<SettingsReader>();
        services.AddScoped<AuditLogReader>();
        services.AddScoped<TrashBin>();

        // Job định kỳ (Host bật scheduler khi Jobs:Enabled ≠ false)
        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            services.AddQuartz(q => q.ScheduleJob<SystemHousekeepingJob>(
                trigger => trigger.WithIdentity("system-housekeeping")
                    .WithCronSchedule(SystemHousekeepingJob.Cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
                job => job.WithIdentity("system-housekeeping")));
        }

        services.AddScoped<IDataSeeder, SystemSeeder>();
        services.AddScoped<IDataSeeder, SettingsSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => SystemEndpoints.Map(group);
}
