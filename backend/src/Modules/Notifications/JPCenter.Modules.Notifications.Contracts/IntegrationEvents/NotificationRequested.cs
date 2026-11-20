using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Notifications.Contracts.IntegrationEvents;

/// <summary>
/// Yêu cầu gửi thông báo trong app (chuông) — TB-04. Phát qua outbox CÙNG giao dịch với thao tác nghiệp vụ; Notifications lưu 1 dòng / người nhận
/// (bỏ trùng) rồi đẩy realtime <c>notification.created</c>. Gửi thông báo không bao giờ làm hỏng thao tác chính.
/// Người nhận = hợp của các nhóm dưới đây, trừ <paramref name="ExcludeAccountId"/>.
/// </summary>
/// <param name="Tone">"info" · "success" · "warning" · "danger"</param>
/// <param name="Link">Đường dẫn trong hệ thống, bắt đầu bằng "/" (vd /admin/academic/requests)</param>
/// <param name="EventKey">Loại sự kiện, vd academic.leave_request.created</param>
/// <param name="StaffAccountIds">System.UserAccount — nhân viên / GV</param>
/// <param name="PortalAccountIds">System.PortalAccount — HV / PH</param>
/// <param name="EmployeeIds">Hr.Employee — tự đổi sang tài khoản đang hoạt động</param>
/// <param name="Permission">Mã quyền đầy đủ, vd "academic.requests.approve" — gửi mọi người đang có quyền đó …</param>
/// <param name="PermissionBranchId">… tại chi nhánh này (null = mọi chi nhánh)</param>
/// <param name="ExcludeAccountId">Người thao tác — không tự báo cho mình (TB-04.E3)</param>
public sealed record NotificationRequested(
    string Title,
    string? Description,
    string Tone,
    string? Link,
    string EventKey,
    IReadOnlyList<Guid>? StaffAccountIds = null,
    IReadOnlyList<Guid>? PortalAccountIds = null,
    IReadOnlyList<Guid>? EmployeeIds = null,
    string? Permission = null,
    Guid? PermissionBranchId = null,
    Guid? ExcludeAccountId = null) : IIntegrationEvent;
