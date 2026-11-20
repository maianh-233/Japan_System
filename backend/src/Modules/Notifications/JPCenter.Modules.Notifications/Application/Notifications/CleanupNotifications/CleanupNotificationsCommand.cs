using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Notifications.Application.Notifications.CleanupNotifications;

/// <summary>
/// TB-05 — job hằng đêm: xóa hẳn thông báo đã đọc quá <see cref="ReadDays"/> ngày và mọi thông báo quá <see cref="MaxDays"/> ngày
/// (cùng quy tắc với <c>Notification.IsStale</c>). Bảng thông báo không phải dữ liệu nghiệp vụ → không xóa mềm.
/// </summary>
public sealed record CleanupNotificationsCommand : ICommand<int>
{
    public const int ReadDays = 90;
    public const int MaxDays = 180;
}

public sealed class CleanupNotificationsHandler(INotificationsDbContext db, IClock clock) : ICommandHandler<CleanupNotificationsCommand, int>
{
    public async Task<Result<int>> Handle(CleanupNotificationsCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var readBefore = now.AddDays(-CleanupNotificationsCommand.ReadDays);
        var createdBefore = now.AddDays(-CleanupNotificationsCommand.MaxDays);
        return await db.Notifications
            .Where(n => n.CreatedAt <= createdBefore || (n.ReadAt != null && n.ReadAt <= readBefore))
            .ExecuteDeleteAsync(ct);
    }
}
