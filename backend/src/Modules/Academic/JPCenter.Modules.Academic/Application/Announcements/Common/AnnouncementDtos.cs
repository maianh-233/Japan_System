using JPCenter.Modules.Academic.Domain.Announcements;

namespace JPCenter.Modules.Academic.Application.Announcements.Common;

/// <summary>Trạng thái thông báo lớp</summary>
public enum AnnouncementState
{
    /// <summary>Chờ hẹn giờ</summary>
    Scheduled,
    /// <summary>Đã gửi</summary>
    Sent,
    /// <summary>Đã hủy (khi còn chờ)</summary>
    Cancelled,
}

/// <summary>Thông báo lớp — khớp <c>ClassAnnouncement</c> (frontend). <c>SentBy</c> = tên người gửi; <c>SentAt</c> null khi đang chờ hẹn giờ.</summary>
public sealed record AnnouncementDto(
    Guid Id,
    Guid ClassId,
    string Title,
    string Content,
    IReadOnlyList<AnnouncementAudience> Audience,
    IReadOnlyList<AnnouncementChannel> Channels,
    bool DebtorsOnly,
    IReadOnlyList<Guid> StudentIds,
    int Recipients,
    int ReadCount,
    string SentBy,
    DateTimeOffset? SentAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? CancelledAt,
    AnnouncementState Status);

/// <summary>Thông báo ở cổng HV / PH</summary>
public sealed record PortalAnnouncementDto(Guid Id, Guid ClassId, string ClassCode, string Title, string Content, string SentBy, DateTimeOffset SentAt,
    bool Read);
