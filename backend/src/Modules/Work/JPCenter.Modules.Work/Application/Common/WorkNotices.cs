using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Work.Application.Common;

/// <summary>
/// Thông báo chuông của Work (TB-04) — phát <see cref="NotificationRequested"/> qua outbox, gọi TRƯỚC <c>SaveChangesAsync</c>; người thao tác không tự nhận.
/// </summary>
public sealed class WorkNotices(IIntegrationEventPublisher events, ICurrentUser user)
{
    public const string TasksPage = "/admin/work/tasks";
    public const string MyTasksPage = "/admin/me/tasks";
    public const string ShiftsPage = "/admin/work/shifts";
    public const string HandoverPage = "/admin/work/handover";

    public void ToEmployees(IEnumerable<Guid> employeeIds, string title, string? description, string tone, string link, string eventKey)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0) return;
        Publish(new NotificationRequested(title, description, tone, link, eventKey, EmployeeIds: ids));
    }

    public void ToPermission(string permission, Guid branchId, string title, string? description, string tone, string link, string eventKey) =>
        Publish(new NotificationRequested(title, description, tone, link, eventKey, Permission: permission, PermissionBranchId: branchId));

    private void Publish(NotificationRequested e) => events.Publish(e with { ExcludeAccountId = user.AccountId });
}
