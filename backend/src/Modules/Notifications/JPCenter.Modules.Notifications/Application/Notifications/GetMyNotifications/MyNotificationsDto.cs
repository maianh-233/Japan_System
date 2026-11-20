using JPCenter.Modules.Notifications.Application.Notifications.Common;

namespace JPCenter.Modules.Notifications.Application.Notifications.GetMyNotifications;

/// <summary>Chuông (TB-01): thông báo mới nhất + tổng số chưa đọc</summary>
public sealed record MyNotificationsDto(IReadOnlyList<NotificationDto> Items, int Unread);
