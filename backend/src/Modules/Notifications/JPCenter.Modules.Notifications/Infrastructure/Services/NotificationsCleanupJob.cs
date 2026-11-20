using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Notifications.Application.Notifications.CleanupNotifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Notifications.Infrastructure.Services;

/// <summary>Job hằng ngày 03:30 giờ Việt Nam — TB-05 dọn thông báo cũ (<see cref="CleanupNotificationsCommand"/>).</summary>
[DisallowConcurrentExecution]
public sealed class NotificationsCleanupJob(ISender sender, ILogger<NotificationsCleanupJob> logger) : IJob
{
    public const string Cron = "0 30 3 * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var deleted = await sender.Send(new CleanupNotificationsCommand(), ct);
        logger.LogInformation("Dọn thông báo: xóa {Count} thông báo cũ", deleted.IsSuccess ? deleted.Value : 0);
    }
}
