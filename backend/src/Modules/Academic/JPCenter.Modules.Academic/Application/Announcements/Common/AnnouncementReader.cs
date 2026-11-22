using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Announcements;

namespace JPCenter.Modules.Academic.Application.Announcements.Common;

/// <summary>Thông báo lớp → DTO kèm tên người gửi</summary>
public sealed class AnnouncementReader(PeopleNames people)
{
    public async Task<IReadOnlyList<AnnouncementDto>> ToDtosAsync(IReadOnlyList<ClassAnnouncement> list, CancellationToken ct)
    {
        var names = await people.OfAsync(list.Select(a => (Guid?)a.SentBy), ct);
        return list.Select(a => new AnnouncementDto(a.Id.Value, a.ClassId.Value, a.Title, a.Content, a.Audience, a.Channels, a.DebtorsOnly, a.StudentIds,
            a.Recipients, a.ReadCount, names.GetValueOrDefault(a.SentBy, "—"), a.SentAt, a.ScheduledAt, a.CancelledAt, StateOf(a))).ToList();
    }

    public static AnnouncementState StateOf(ClassAnnouncement a) =>
        a.CancelledAt is not null ? AnnouncementState.Cancelled : a.SentAt is not null ? AnnouncementState.Sent : AnnouncementState.Scheduled;
}
