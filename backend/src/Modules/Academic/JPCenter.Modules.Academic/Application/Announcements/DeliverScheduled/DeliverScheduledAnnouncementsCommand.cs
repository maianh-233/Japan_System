using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Announcements.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Application.Announcements.DeliverScheduled;

/// <summary>
/// ND-07 — job mỗi phút: gửi thông báo lớp đã tới giờ hẹn. Tới giờ mà không còn người nhận (E1 — vd hết HV nợ phí) → tự hủy, ghi log.
/// </summary>
public sealed record DeliverScheduledAnnouncementsCommand : ICommand<int>;

public sealed class DeliverScheduledAnnouncementsHandler(IAcademicDbContext db, AnnouncementSender sender, IClock clock,
    ILogger<DeliverScheduledAnnouncementsHandler> logger) : ICommandHandler<DeliverScheduledAnnouncementsCommand, int>
{
    public async Task<Result<int>> Handle(DeliverScheduledAnnouncementsCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.ClassAnnouncements
            .Where(a => a.SentAt == null && a.CancelledAt == null && a.ScheduledAt != null && a.ScheduledAt <= now)
            .OrderBy(a => a.ScheduledAt).Take(100).ToListAsync(ct);
        var sent = 0;
        foreach (var a in due)
        {
            var r = await sender.SendAsync(a, now, ct);
            if (r.IsSuccess) sent++;
            else
            {
                a.Cancel(now);
                logger.LogWarning("Thông báo lớp {AnnouncementId} tới giờ hẹn nhưng không gửi được: {Error}", a.Id.Value, r.Error.Message);
            }
            await db.SaveChangesAsync(ct);
        }
        return sent;
    }
}
