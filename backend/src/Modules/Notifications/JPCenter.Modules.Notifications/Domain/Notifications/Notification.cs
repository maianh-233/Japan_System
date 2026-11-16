using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Notifications.Domain.Notifications;

/// <summary>Thông báo trong app (chuông, realtime qua SignalR) cho nhân viên / GV / HV / PH. Bảng: notifications.notifications</summary>
public sealed class Notification : AggregateRoot<NotificationId>
{
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

    public static Notification Create(RecipientType recipientType, Guid recipientId, string title, string description, NotificationTone tone,
        string? link, string? eventKey, DateTimeOffset at) =>
        new()
        {
            Id = NotificationId.New(), RecipientType = recipientType, RecipientId = recipientId, Title = title, Description = description, Tone = tone,
            Link = link, EventKey = eventKey, CreatedAt = at,
        };

    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;
}
