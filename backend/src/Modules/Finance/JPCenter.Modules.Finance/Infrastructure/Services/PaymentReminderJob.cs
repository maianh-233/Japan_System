using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Debts.AutoRemind;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>Job hằng ngày 08:00 giờ Việt Nam (Quartz): tự nhắc học phí theo mốc trước hạn / đúng hạn / quá hạn (TC-11 bước 4)</summary>
[DisallowConcurrentExecution]
public sealed class PaymentReminderJob(ISender sender, ILogger<PaymentReminderJob> logger) : IJob
{
    public const string Cron = "0 0 8 * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var sent = await sender.Send(new AutoRemindCommand(), ct);
        logger.LogInformation("Tự nhắc học phí: {Count} hóa đơn", sent.IsSuccess ? sent.Value : 0);
    }
}
