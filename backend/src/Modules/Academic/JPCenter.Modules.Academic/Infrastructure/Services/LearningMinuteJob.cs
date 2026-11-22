using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Announcements.DeliverScheduled;
using JPCenter.Modules.Academic.Application.Exams.AutoSubmitExams;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Job mỗi phút (Quartz): gửi thông báo lớp tới giờ hẹn (ND-07) · tự nộp bài thi online quá giờ (ND-05.E4)</summary>
[DisallowConcurrentExecution]
public sealed class LearningMinuteJob(ISender sender, ILogger<LearningMinuteJob> logger) : IJob
{
    public const string Cron = "0 * * * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var sent = await sender.Send(new DeliverScheduledAnnouncementsCommand(), ct);
        if (sent is { IsSuccess: true, Value: > 0 }) logger.LogInformation("Thông báo lớp hẹn giờ: đã gửi {Count}", sent.Value);
        var submitted = await sender.Send(new AutoSubmitExamsCommand(), ct);
        if (submitted is { IsSuccess: true, Value: > 0 }) logger.LogInformation("Bài thi online quá giờ: tự nộp {Count} bài", submitted.Value);
    }
}
