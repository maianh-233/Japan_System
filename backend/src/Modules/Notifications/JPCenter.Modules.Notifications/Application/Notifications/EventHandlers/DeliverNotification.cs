using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Realtime;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Application.Notifications.Common;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;
using JPCenter.Modules.Notifications.Domain.Notifications;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Notifications.Application.Notifications.EventHandlers;

/// <summary>
/// TB-04 — nhận <see cref="NotificationRequested"/>: xác định người nhận (tài khoản, nhân viên, theo quyền), lưu 1 dòng / người, đẩy realtime
/// <c>notification.created</c> (TB-03). Không có người nhận (E1) hoặc nội dung sai (E2) → ghi log, bỏ qua (không thử lại).
/// Lưu xong mới đẩy realtime; đẩy lỗi chỉ ghi log — người nhận vẫn thấy ở lần tải chuông sau.
/// </summary>
public sealed class DeliverNotification(INotificationsDbContext db, IStaffAccessDirectory staff, IRealtimeNotifier realtime, IClock clock,
    ILogger<DeliverNotification> logger) : IIntegrationEventHandler<NotificationRequested>
{
    public const string RealtimeType = "notification.created";

    public async Task Handle(NotificationRequested e, CancellationToken ct)
    {
        var staffIds = new HashSet<Guid>(e.StaffAccountIds ?? []);
        var portalIds = new HashSet<Guid>(e.PortalAccountIds ?? []);

        if (e.EmployeeIds is { Count: > 0 } employees)
            staffIds.UnionWith((await staff.ActiveAccountsOfAsync(employees, ct)).Values);
        if (!string.IsNullOrWhiteSpace(e.Permission))
            staffIds.UnionWith((await staff.WithPermissionAsync(e.Permission, e.PermissionBranchId, ct)).Select(c => c.AccountId));

        if (e.ExcludeAccountId is { } exclude)
        {
            staffIds.Remove(exclude);
            portalIds.Remove(exclude);
        }
        if (staffIds.Count + portalIds.Count == 0)
        {
            logger.LogWarning("Thông báo {EventKey} không có người nhận", e.EventKey);
            return;
        }

        var now = clock.UtcNow;
        var tone = NotificationMapping.ParseTone(e.Tone);
        var created = new List<Notification>();
        foreach (var (type, id) in staffIds.Select(id => (RecipientType.Staff, id)).Concat(portalIds.Select(id => (RecipientType.Portal, id))))
        {
            var n = Notification.Create(type, id, e.Title, e.Description, tone, e.Link, e.EventKey, now);
            if (n.IsFailure)
            {
                logger.LogError("Thông báo {EventKey} không hợp lệ: {Error}", e.EventKey, n.Error.Message);
                return;
            }
            created.Add(n.Value);
        }

        db.Notifications.AddRange(created);
        await db.SaveChangesAsync(ct);

        foreach (var n in created) await realtime.ToAccountsAsync([n.RecipientId], RealtimeType, n.ToDto(), ct);
    }
}
