using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Application.Notifications.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Notifications.Application.Notifications.MarkAllRead;

/// <summary>TB-02 — đọc hết: 1 câu UPDATE cho mọi thông báo chưa đọc của mình. Trả số dòng đã đánh dấu.</summary>
public sealed record MarkAllReadCommand : ICommand<int>;

public sealed class MarkAllReadHandler(INotificationsDbContext db, ICurrentUser user, IClock clock) : ICommandHandler<MarkAllReadCommand, int>
{
    public async Task<Result<int>> Handle(MarkAllReadCommand cmd, CancellationToken ct)
    {
        if (Recipient.Of(user) is not { } me) return 0;
        var now = clock.UtcNow;
        return await db.Notifications
            .Where(n => n.RecipientType == me.Type && n.RecipientId == me.AccountId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }
}
