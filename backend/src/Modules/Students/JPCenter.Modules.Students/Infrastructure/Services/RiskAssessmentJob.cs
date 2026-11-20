using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.CareCases.RecalculateRisk;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Students.Infrastructure.Services;

/// <summary>Job hằng đêm 02:00 giờ Việt Nam (Quartz): tính lại nguy cơ bỏ học cho mọi HV đang học (HV-14.E4)</summary>
[DisallowConcurrentExecution]
public sealed class RiskAssessmentJob(ISender sender, ILogger<RiskAssessmentJob> logger) : IJob
{
    public const string Cron = "0 0 2 * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var flagged = await sender.Send(new RecalculateRiskCommand(), ct);
        logger.LogInformation("Cảnh báo bỏ học: {Count} HV × lớp có dấu hiệu", flagged.IsSuccess ? flagged.Value : 0);
    }
}
