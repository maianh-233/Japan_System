using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

public static class SessionErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.session.not_found", "Không tìm thấy buổi học");
    public static readonly Error ClassPaused = Error.Conflict("academic.class.paused", "Lớp đang tạm dừng");
    public static readonly Error InvalidTime = Error.Validation("academic.session.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu");
    public static readonly Error MakeupForRequired = Error.Validation("academic.session.makeup_for_required", "Buổi học bù phải chỉ ra buổi bị hủy được bù");
    public static readonly Error ReasonRequired = Error.Validation("academic.session.reason_required", "Cần ghi lý do");
    public static readonly Error NotScheduled = Error.Conflict("academic.session.not_scheduled", "Buổi học đã diễn ra hoặc đã hủy");
    public static readonly Error Cancelled = Error.Conflict("academic.session.cancelled", "Buổi học đã bị hủy");
    public static readonly Error AlreadyStarted = Error.Conflict("academic.session.already_started", "Buổi học đã bắt đầu — không thể hủy (dùng báo sự cố)");
    public static readonly Error NotStarted = Error.Conflict("academic.session.not_started", "Buổi học chưa bắt đầu — dùng dời lịch / hủy buổi");
    public static readonly Error NotFinished = Error.Conflict("academic.session.not_finished", "Buổi học chưa tới giờ kết thúc");
    public static readonly Error NotAttended = Error.Conflict("academic.session.not_attended", "Chưa điểm danh học viên nào");
    public static readonly Error AlreadyConfirmed = Error.Conflict("academic.session.already_confirmed", "Buổi học đã được xác nhận");
    public static readonly Error NotConfirmed = Error.Conflict("academic.session.not_confirmed", "Buổi học chưa được xác nhận");
    public static readonly Error ConfirmedLocked = Error.Conflict("academic.session.confirmed_locked", "Buổi đã xác nhận — giáo viên không sửa được, liên hệ giáo vụ");
    public static readonly Error ReportLocked = Error.Conflict("academic.session.report_locked", "Đã quá 24 giờ sau khi xác nhận — không sửa được báo cáo buổi");
    public static readonly Error NoIncident = Error.NotFound("academic.session.no_incident", "Buổi học không có sự cố");
    public static readonly Error NotCheckedIn = Error.Conflict("academic.teacher_attendance.not_checked_in", "Chưa check-in — không check-out được");
    public static readonly Error AlreadyMadeUp = Error.Conflict("academic.session.already_made_up", "Buổi này đã có buổi học bù khác");
    public static readonly Error MakeupTargetInvalid = Error.Conflict("academic.session.makeup_target", "Chỉ xếp học bù cho buổi đã hủy của cùng lớp");
}
