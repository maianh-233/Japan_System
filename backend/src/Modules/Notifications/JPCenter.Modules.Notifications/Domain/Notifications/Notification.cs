using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Notifications.Domain.Notifications;

/// <summary>Thông báo trong app (chuông, realtime qua SignalR) cho nhân viên / GV / HV / PH. Bảng: notifications.notifications</summary>
public sealed class Notification : AggregateRoot<NotificationId>
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int LinkMaxLength = 500;

    private Notification() { }

    public RecipientType RecipientType { get; private set; }
    /// <summary>System.UserAccount hoặc System.PortalAccount</summary>
    public Guid RecipientId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public NotificationTone Tone { get; private set; }
    /// <summary>Đường dẫn mở khi bấm, vd /admin/crm/leads?id=...</summary>
    public string? Link { get; private set; }
    /// <summary>Sự kiện sinh thông báo, vd crm.lead.sla_breached — để lọc / tắt theo loại</summary>
    public string? EventKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>
    /// Tạo 1 thông báo cho 1 người nhận (TB-04). Tiêu đề bắt buộc; nội dung dài bị cắt (thông báo chỉ là lối tắt, không chặn nghiệp vụ);
    /// link chỉ nhận đường dẫn nội bộ bắt đầu bằng "/".
    /// </summary>
    public static Result<Notification> Create(RecipientType recipientType, Guid recipientId, string title, string? description, NotificationTone tone,
        string? link, string? eventKey, DateTimeOffset at)
    {
        if (recipientId == Guid.Empty) return NotificationErrors.RecipientRequired;
        if (string.IsNullOrWhiteSpace(title)) return NotificationErrors.TitleRequired;
        if (!string.IsNullOrWhiteSpace(link) && (!link.StartsWith('/') || link.StartsWith("//", StringComparison.Ordinal)))
            return NotificationErrors.LinkInvalid;

        return new Notification
        {
            Id = NotificationId.New(),
            RecipientType = recipientType,
            RecipientId = recipientId,
            Title = Cut(title.Trim(), TitleMaxLength),
            Description = Cut(description?.Trim() ?? string.Empty, DescriptionMaxLength),
            Tone = tone,
            Link = string.IsNullOrWhiteSpace(link) ? null : Cut(link.Trim(), LinkMaxLength),
            EventKey = string.IsNullOrWhiteSpace(eventKey) ? null : eventKey.Trim(),
            CreatedAt = at,
        };
    }

    /// <summary>Đánh dấu đã đọc (TB-02) — đọc lại không đổi thời điểm đọc đầu tiên. Trả <c>true</c> khi vừa chuyển sang đã đọc.</summary>
    public bool MarkRead(DateTimeOffset at)
    {
        if (ReadAt is not null) return false;
        ReadAt = at;
        return true;
    }

    /// <summary>Đủ điều kiện dọn (TB-05): đã đọc quá <paramref name="readDays"/> ngày, hoặc tạo quá <paramref name="maxDays"/> ngày.</summary>
    public bool IsStale(DateTimeOffset now, int readDays, int maxDays) =>
        CreatedAt <= now.AddDays(-maxDays) || (ReadAt is { } read && read <= now.AddDays(-readDays));

    private static string Cut(string value, int max) => value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
}
