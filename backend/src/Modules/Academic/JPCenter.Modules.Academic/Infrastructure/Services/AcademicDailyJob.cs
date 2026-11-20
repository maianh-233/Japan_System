using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Promotions.ExpirePromotionHolds;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Job hằng ngày 00:30 giờ Việt Nam (Quartz): trả chỗ giữ lên lớp quá hạn (DT-17.E3)</summary>
[DisallowConcurrentExecution]
public sealed class AcademicDailyJob(ISender sender, ILogger<AcademicDailyJob> logger) : IJob
{
    public const string Cron = "0 30 0 * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var released = await sender.Send(new ExpirePromotionHoldsCommand(), ct);
        logger.LogInformation("Đào tạo hằng ngày: trả {Count} chỗ giữ lên lớp quá hạn", released.IsSuccess ? released.Value : 0);
    }
}
