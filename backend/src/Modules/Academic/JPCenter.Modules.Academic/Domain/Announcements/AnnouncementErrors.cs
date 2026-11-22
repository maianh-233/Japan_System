using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Announcements;

/// <summary>Lỗi thông báo lớp (ND-07)</summary>
public static class AnnouncementErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.announcement.not_found", "Không tìm thấy thông báo");
    public static readonly Error ContentRequired = Error.Validation("academic.announcement.content_required", "Nhập tiêu đề và nội dung");
    public static readonly Error TooLong = Error.Validation("academic.announcement.too_long", "Tiêu đề tối đa 200, nội dung tối đa 5000 ký tự");
    public static readonly Error TargetRequired = Error.Validation("academic.announcement.target_required", "Chọn người nhận và kênh gửi");
    /// <summary>ND-07.E1</summary>
    public static readonly Error NoRecipients = Error.Validation("academic.announcement.no_recipients", "Không có người nhận");
    /// <summary>ND-07.E2</summary>
    public static readonly Error InvalidSchedule = Error.Validation("academic.announcement.invalid_schedule", "Giờ hẹn gửi phải ở tương lai");
    /// <summary>ND-07.E3</summary>
    public static readonly Error NotPending = Error.Conflict("academic.announcement.not_pending", "Thông báo đã gửi hoặc đã hủy");
}
