namespace JPCenter.Modules.Notifications.Application.Notifications.Common;

/// <summary>1 thông báo — khớp <c>AppNotification</c> ở <c>frontend/src/types/auth.ts</c>.</summary>
/// <param name="Tone">"info" · "success" · "warning" · "danger"</param>
public sealed record NotificationDto(Guid Id, string Title, string Description, DateTimeOffset CreatedAt, bool Read, string Tone, string? Link);
