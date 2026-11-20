using System.Text.Json;
using JPCenter.Modules.Notifications.Domain.Notifications;

namespace JPCenter.Modules.Notifications.Application.Notifications.Common;

/// <summary>Đổi entity → DTO, tone ↔ chuỗi</summary>
public static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id.Value, n.Title, n.Description, n.CreatedAt, n.ReadAt != null, ToneText(n.Tone), n.Link);

    public static string ToneText(NotificationTone tone) => JsonNamingPolicy.SnakeCaseLower.ConvertName(tone.ToString());

    /// <summary>Tone lạ → info (không làm hỏng việc gửi)</summary>
    public static NotificationTone ParseTone(string? tone) => tone switch
    {
        "success" => NotificationTone.Success,
        "warning" => NotificationTone.Warning,
        "danger" => NotificationTone.Danger,
        _ => NotificationTone.Info,
    };
}
