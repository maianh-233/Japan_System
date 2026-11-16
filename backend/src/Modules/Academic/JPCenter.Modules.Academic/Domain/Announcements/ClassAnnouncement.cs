using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.Announcements;

/// <summary>Thông báo gửi cho lớp (HV / PH / GV) qua cổng / Zalo / email, có thể hẹn giờ. Bảng: academic.class_announcements</summary>
public sealed class ClassAnnouncement : AggregateRoot<ClassAnnouncementId>, IAuditable
{
    private ClassAnnouncement() { }

    public ClassId ClassId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public List<AnnouncementAudience> Audience { get; private set; } = [];
    public List<AnnouncementChannel> Channels { get; private set; } = [];
    /// <summary>Chỉ gửi HV còn nợ học phí</summary>
    public bool DebtorsOnly { get; private set; }
    /// <summary>Chỉ gửi các HV này (+ phụ huynh) — rỗng = cả lớp</summary>
    public List<Guid> StudentIds { get; private set; } = [];
    /// <summary>Số người nhận lúc gửi</summary>
    public int Recipients { get; private set; }
    public int ReadCount { get; private set; }
    public Guid SentBy { get; private set; }
    /// <summary>Lúc gửi thật — null khi đang chờ hẹn giờ</summary>
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }

    public static Result<ClassAnnouncement> Create(ClassId classId, string title, string content, List<AnnouncementAudience> audience,
        List<AnnouncementChannel> channels, bool debtorsOnly, List<Guid> studentIds, Guid sentBy, DateTimeOffset? scheduledAt)
    {
        if (audience.Count == 0 || channels.Count == 0)
            return Error.Validation("academic.announcement.target_required", "Chọn người nhận và kênh gửi");
        return new ClassAnnouncement
        {
            Id = ClassAnnouncementId.New(), ClassId = classId, Title = title, Content = content, Audience = audience, Channels = channels,
            DebtorsOnly = debtorsOnly, StudentIds = studentIds, SentBy = sentBy, ScheduledAt = scheduledAt,
        };
    }

    public void MarkSent(int recipients, DateTimeOffset at) => (Recipients, SentAt) = (recipients, at);
    public void IncreaseRead() => ReadCount++;
}
