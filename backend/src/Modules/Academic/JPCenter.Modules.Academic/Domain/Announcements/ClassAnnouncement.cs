using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.Announcements;

/// <summary>
/// Thông báo gửi cho lớp (ND-07) tới HV / PH / GV qua cổng (Zalo / email: giai đoạn 22), hẹn giờ được; chỉ HV nợ phí / chỉ HV được chọn.
/// Đã gửi thì không sửa / hủy; chờ hẹn giờ thì hủy được. Đếm đã xem mỗi người 1 lần. Bảng: academic.class_announcements
/// </summary>
public sealed class ClassAnnouncement : AggregateRoot<ClassAnnouncementId>, IAuditable
{
    public const int MaxTitle = 200;
    public const int MaxContent = 5000;

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
    /// <summary>Tài khoản đã xem (System.UserAccount) — mỗi người tính 1 lần (E4)</summary>
    public List<Guid> ReadBy { get; private set; } = [];
    /// <summary>Người gửi (Hr.Employee)</summary>
    public Guid SentBy { get; private set; }
    /// <summary>Lúc gửi thật — null khi đang chờ hẹn giờ</summary>
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    /// <summary>Hủy khi còn chờ hẹn giờ (E3)</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public bool IsPending => SentAt is null && CancelledAt is null;

    public static Result<ClassAnnouncement> Create(ClassId classId, string? title, string? content, List<AnnouncementAudience> audience,
        List<AnnouncementChannel> channels, bool debtorsOnly, List<Guid> studentIds, Guid sentBy, DateTimeOffset? scheduledAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content)) return AnnouncementErrors.ContentRequired;
        if (title.Trim().Length > MaxTitle || content.Trim().Length > MaxContent) return AnnouncementErrors.TooLong;
        if (audience.Count == 0 || channels.Count == 0) return AnnouncementErrors.TargetRequired;
        if (scheduledAt is { } at && at <= now) return AnnouncementErrors.InvalidSchedule;
        return new ClassAnnouncement
        {
            Id = ClassAnnouncementId.New(), ClassId = classId, Title = title.Trim(), Content = content.Trim(), Audience = audience.Distinct().ToList(),
            Channels = channels.Distinct().ToList(), DebtorsOnly = debtorsOnly, StudentIds = studentIds.Distinct().ToList(), SentBy = sentBy,
            ScheduledAt = scheduledAt,
        };
    }

    /// <summary>Gửi (ngay hoặc tới giờ hẹn) — không có người nhận → E1</summary>
    public Result MarkSent(int recipients, DateTimeOffset at)
    {
        if (!IsPending) return AnnouncementErrors.NotPending;
        if (recipients <= 0) return AnnouncementErrors.NoRecipients;
        (Recipients, SentAt) = (recipients, at);
        return Result.Success();
    }

    /// <summary>E3 — hủy thông báo chờ hẹn giờ</summary>
    public Result Cancel(DateTimeOffset at)
    {
        if (!IsPending) return AnnouncementErrors.NotPending;
        CancelledAt = at;
        return Result.Success();
    }

    /// <summary>E4 — người nhận mở thông báo trên cổng; trả false nếu đã đếm trước đó</summary>
    public bool MarkRead(Guid accountId)
    {
        if (SentAt is null || ReadBy.Contains(accountId)) return false;
        ReadBy = [.. ReadBy, accountId];
        ReadCount = ReadBy.Count;
        return true;
    }
}
