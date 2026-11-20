using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Requests.ExpireInvites;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Job mỗi 5 phút (Quartz): lời mời dạy thay quá hạn trả lời → hết hiệu lực, báo giáo vụ (DT-16 bước 3)</summary>
[DisallowConcurrentExecution]
public sealed class SubstituteInviteJob(ISender sender, ILogger<SubstituteInviteJob> logger) : IJob
{
    public const string Cron = "0 0/5 * * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var expired = await sender.Send(new ExpireInvitesCommand(), ct);
        if (expired is { IsSuccess: true, Value: > 0 }) logger.LogInformation("Lời mời dạy thay: {Count} lời mời hết hạn trả lời", expired.Value);
    }
}
