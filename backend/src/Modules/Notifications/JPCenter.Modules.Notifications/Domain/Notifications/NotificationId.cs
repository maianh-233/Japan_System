using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Notifications.Domain.Notifications;

/// <summary>Id thông báo trong app</summary>
public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(IdGenerator.NewId());
}
