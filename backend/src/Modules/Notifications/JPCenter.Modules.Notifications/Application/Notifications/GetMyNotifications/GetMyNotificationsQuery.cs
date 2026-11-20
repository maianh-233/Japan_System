using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Application.Notifications.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Notifications.Application.Notifications.GetMyNotifications;

/// <summary>TB-01 — thông báo của chính tài khoản đang đăng nhập, mới nhất trước.</summary>
/// <param name="Before">Xem cũ hơn: <c>createdAt</c> của dòng cuối đã có</param>
/// <param name="Limit">Số dòng (1 → 50, mặc định 50)</param>
public sealed record GetMyNotificationsQuery(DateTimeOffset? Before, int? Limit) : IQuery<MyNotificationsDto>;

public sealed class GetMyNotificationsHandler(INotificationsDbContext db, ICurrentUser user) : IQueryHandler<GetMyNotificationsQuery, MyNotificationsDto>
{
    public const int MaxLimit = 50;

    public async Task<Result<MyNotificationsDto>> Handle(GetMyNotificationsQuery query, CancellationToken ct)
    {
        if (Recipient.Of(user) is not { } me) return new MyNotificationsDto([], 0);

        var mine = db.Notifications.AsNoTracking().Where(n => n.RecipientType == me.Type && n.RecipientId == me.AccountId);
        var page = mine;
        if (query.Before is { } before) page = page.Where(n => n.CreatedAt < before);
        var limit = Math.Clamp(query.Limit ?? MaxLimit, 1, MaxLimit);

        var items = await page.OrderByDescending(n => n.CreatedAt).Take(limit).ToListAsync(ct);
        var unread = await mine.CountAsync(n => n.ReadAt == null, ct);
        return new MyNotificationsDto(items.Select(n => n.ToDto()).ToList(), unread);
    }
}
