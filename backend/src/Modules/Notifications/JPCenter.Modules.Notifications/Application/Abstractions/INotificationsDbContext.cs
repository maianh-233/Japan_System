using JPCenter.Modules.Notifications.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Notifications.Application.Abstractions;

/// <summary>DbContext module Notifications cho tầng Application (Infrastructure hiện thực bằng NotificationsDbContext).</summary>
public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
