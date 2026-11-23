using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Tasks.RemindDueTasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Work.Infrastructure.Services;

/// <summary>Job hằng ngày 07:30 giờ Việt Nam (Quartz): nhắc hạn task (CV-06).</summary>
[DisallowConcurrentExecution]
public sealed class WorkDailyJob(ISender sender, ILogger<WorkDailyJob> logger) : IJob
{
    public const string Cron = "0 30 7 * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var reminded = await sender.Send(new RemindDueTasksCommand(), ct);
        logger.LogInformation("Nhắc hạn task: {People} người", reminded.IsSuccess ? reminded.Value : 0);
    }
}
