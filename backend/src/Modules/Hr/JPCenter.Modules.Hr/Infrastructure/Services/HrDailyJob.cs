using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Employees.ApplyScheduledQuits;
using JPCenter.Modules.Hr.Application.Employees.RemindExpiringContracts;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Hr.Infrastructure.Services;

/// <summary>
/// Job hằng ngày 01:00 giờ Việt Nam (Quartz): áp dụng nghỉ việc đã hẹn (NS-07.E6) rồi nhắc hợp đồng còn 45 / 15 / 7 ngày (NS-09.E1).
/// </summary>
[DisallowConcurrentExecution]
public sealed class HrDailyJob(ISender sender, ILogger<HrDailyJob> logger) : IJob
{
    public const string Cron = "0 0 1 * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var quits = await sender.Send(new ApplyScheduledQuitsCommand(), ct);
        var reminders = await sender.Send(new RemindExpiringContractsCommand(), ct);
        logger.LogInformation("Nhân sự hằng ngày: {Quits} nghỉ việc theo lịch hẹn, nhắc {Contracts} hợp đồng sắp hết hạn",
            quits.IsSuccess ? quits.Value : 0, reminders.IsSuccess ? reminders.Value : 0);
    }
}
