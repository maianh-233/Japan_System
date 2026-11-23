using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Hr.Application.Common;

/// <summary>
/// Thông báo chuông của Hr (TB-04) — phát <see cref="NotificationRequested"/> qua outbox. Gọi TRƯỚC <c>SaveChangesAsync</c>
/// để thông báo đi cùng giao dịch; người thao tác không tự nhận.
/// </summary>
public sealed class HrNotices(IIntegrationEventPublisher events, ICurrentUser user)
{
    public const string RequestsPage = "/admin/hr/requests";
    public const string MyAttendancePage = "/admin/me/attendance";
    public const string RecruitmentPage = "/admin/hr/recruitment";
    public const string OnboardingPage = "/admin/hr/onboarding";

    /// <summary>Gửi các nhân viên (Hr.Employee) — tự đổi sang tài khoản đang hoạt động</summary>
    public void ToEmployees(IReadOnlyList<Guid> employeeIds, string title, string? description, string tone, string link, string eventKey)
    {
        if (employeeIds.Count == 0) return;
        Publish(new NotificationRequested(title, description, tone, link, eventKey, EmployeeIds: employeeIds));
    }

    /// <summary>Gửi mọi người có quyền <paramref name="permission"/> tại chi nhánh</summary>
    public void ToPermission(string permission, Guid branchId, string title, string? description, string tone, string link, string eventKey) =>
        Publish(new NotificationRequested(title, description, tone, link, eventKey, Permission: permission, PermissionBranchId: branchId));

    private void Publish(NotificationRequested e) => events.Publish(e with { ExcludeAccountId = user.AccountId });
}
