using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Notifications.Domain.Notifications;

public static class NotificationErrors
{
    public static readonly Error RecipientRequired = Error.Validation("notifications.recipient_required", "Thông báo chưa có người nhận");
    public static readonly Error TitleRequired = Error.Validation("notifications.title_required", "Thông báo chưa có tiêu đề");
    public static readonly Error LinkInvalid = Error.Validation("notifications.link_invalid", "Đường dẫn thông báo phải là đường dẫn trong hệ thống (bắt đầu bằng /)");
    /// <summary>Không có, hoặc không phải của người đang đăng nhập (không lộ có tồn tại — TB-02)</summary>
    public static readonly Error NotFound = Error.NotFound("notifications.not_found", "Không tìm thấy thông báo");
}
