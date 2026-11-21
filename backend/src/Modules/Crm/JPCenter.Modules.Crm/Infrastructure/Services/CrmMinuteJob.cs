using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Appointments.RemindAppointments;
using JPCenter.Modules.Crm.Application.LeadRules.CheckSla;
using JPCenter.Modules.Crm.Application.PlacementTests.ExpireAttempts;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>
/// Job Quartz mỗi phút: quét SLA (CRM-11). Mỗi 5 phút: nhắc lịch hẹn trước 60 phút (CRM-14). Đầu mỗi giờ: hết hạn link test (CRM-13.E7).
/// </summary>
[DisallowConcurrentExecution]
public sealed class CrmMinuteJob(ISender sender, ILogger<CrmMinuteJob> logger) : IJob
{
    public const string Cron = "0 * * * * ?";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var at = context.FireTimeUtc;
        var sla = await sender.Send(new CheckSlaCommand(), ct);
        if (sla is { IsSuccess: true, Value: { } s } && (s.Escalated > 0 || s.Reassigned > 0))
            logger.LogInformation("SLA lead: cảnh báo {Escalated}, tự chuyển {Reassigned}", s.Escalated, s.Reassigned);

        if (at.Minute % 5 == 0)
        {
            var reminded = await sender.Send(new RemindAppointmentsCommand(), ct);
            if (reminded is { IsSuccess: true, Value: > 0 }) logger.LogInformation("Nhắc lịch hẹn: {Count}", reminded.Value);
        }
        if (at.Minute == 0)
        {
            var expired = await sender.Send(new ExpireAttemptsCommand(), ct);
            if (expired is { IsSuccess: true, Value: > 0 }) logger.LogInformation("Link test hết hạn: {Count}", expired.Value);
        }
    }
}
