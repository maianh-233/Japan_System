using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Application.Notifications.Common;
using JPCenter.Modules.Notifications.Domain.Notifications;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Notifications.Application.Notifications.MarkRead;

/// <summary>TB-02 — đánh dấu 1 thông báo đã đọc. Của người khác → 404 như không tồn tại.</summary>
public sealed record MarkReadCommand(Guid Id) : ICommand;

public sealed class MarkReadHandler(INotificationsDbContext db, ICurrentUser user, IClock clock) : ICommandHandler<MarkReadCommand>
{
    public async Task<Result> Handle(MarkReadCommand cmd, CancellationToken ct)
    {
        if (Recipient.Of(user) is not { } me) return NotificationErrors.NotFound;
        var id = new NotificationId(cmd.Id);
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientType == me.Type && x.RecipientId == me.AccountId, ct);
        if (n is null) return NotificationErrors.NotFound;
        if (n.MarkRead(clock.UtcNow)) await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
