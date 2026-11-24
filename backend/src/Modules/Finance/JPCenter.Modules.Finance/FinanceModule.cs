using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Finance.Api;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.Modules.Finance.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Finance.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace JPCenter.Modules.Finance;

/// <summary>
/// Điểm vào module Finance (Tài chính, user story 08) — schema <c>finance</c>, API <c>/api/v1/finance</c>.
/// Giai đoạn 19: bảng giá, ưu đãi, đóng đợt, hóa đơn từ ghi danh, thu / hoàn / hủy (duyệt 2 người), đối soát VietQR, công nợ + tự nhắc, cổng HV / PH.
/// </summary>
public sealed class FinanceModule : IModule
{
    public string Name => FinanceDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<FinanceDbContext>(configuration, FinanceDbContext.SchemaName);
        services.AddScoped<IFinanceDbContext>(sp => sp.GetRequiredService<FinanceDbContext>());
        services.AddHandlersFromAssembly(typeof(FinanceModule).Assembly);

        // Cho module khác (Finance.Contracts)
        services.AddScoped<IInvoiceApprovals, InvoiceApprovals>();
        services.AddScoped<ITuitionQuery, TuitionQuery>();
        services.AddScoped<ICashLedger, CashLedger>();

        // Dùng chung giữa các use case
        services.AddScoped<IInvoiceNumbering, InvoiceNumbering>();
        services.AddScoped<FinanceAccess>();
        services.AddScoped<PeopleNames>();
        services.AddScoped<InvoiceReader>();
        services.AddScoped<InvoiceFinder>();
        services.AddScoped<BankMatcher>();
        services.AddScoped<TransactionFinder>();

        services.AddScoped<IDataSeeder, FinanceSeeder>();
        services.AddScoped<IDataSeeder, PayrollSeeder>();

        if (JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            services.AddQuartz(q => q.ScheduleJob<PaymentReminderJob>(
                trigger => trigger.WithIdentity("finance-reminders")
                    .WithCronSchedule(PaymentReminderJob.Cron, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
                job => job.WithIdentity("finance-reminders")));
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => FinanceEndpoints.Map(group);
}
