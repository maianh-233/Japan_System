using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Classes;

public static class ClassErrors
{
    public static readonly Error TeacherCount = Error.Validation("academic.class.teacher_count", "Lớp cần 1 hoặc 2 giáo viên");
    public static readonly Error MixedTeachers = Error.Validation("academic.class.teacher_count", "Lớp Việt + Nhật cần 1 GV Việt và 1 GV Nhật");
    public static readonly Error InvalidTime = Error.Validation("academic.class.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu");
    public static readonly Error InvalidWeekdays = Error.Validation("academic.class.invalid_weekdays", "Chọn thứ học từ Thứ 2 đến Chủ nhật");
    public static readonly Error InvalidSize = Error.Validation("academic.class.invalid_size", "Sĩ số tối đa phải ≥ tối thiểu ≥ 1");
    public static readonly Error InvalidDates = Error.Validation("academic.class.invalid_dates", "Ngày kết thúc phải sau ngày khai giảng");
    public static readonly Error InvalidHours = Error.Validation("academic.class.invalid_hours", "Tổng giờ học phải lớn hơn 0");
    public static readonly Error InvalidFee = Error.Validation("academic.class.invalid_fee", "Học phí không được âm");
    public static readonly Error NameRequired = Error.Validation("academic.class.name_required", "Nhập tên lớp");
    public static readonly Error RoomRequired = Error.Validation("academic.class.room_required", "Lớp offline cần chọn phòng");
    public static readonly Error MeetingUrlRequired = Error.Validation("academic.class.meeting_url_required", "Lớp online cần link học");
    public static readonly Error PauseReason = Error.Validation("academic.class.pause_reason", "Cần ghi lý do tạm dừng");
    public static readonly Error CancelReason = Error.Validation("academic.class.cancel_reason", "Cần ghi lý do hủy lớp");
    public static readonly Error NotEnrolling = Error.Conflict("academic.class.not_enrolling", "Lớp không ở trạng thái chờ khai giảng");
    public static readonly Error NotOngoing = Error.Conflict("academic.class.not_ongoing", "Lớp không ở trạng thái đang học");
    public static readonly Error NotPaused = Error.Conflict("academic.class.not_paused", "Lớp không ở trạng thái tạm dừng");
    public static readonly Error Closed = Error.Conflict("academic.class.closed", "Lớp đã kết thúc hoặc đã hủy — chỉ sửa được ghi chú");
    public static readonly Error Locked = Error.Conflict("academic.class.locked", "Lớp đã khai giảng — không đổi khóa, lộ trình, mã lớp, chi nhánh");
    public static readonly Error NotEnoughStudents = Error.Conflict("academic.class.not_enough_students", "Lớp chưa có học viên nào — chưa khai giảng được");
    public static readonly Error Full = Error.Conflict("academic.class.full", "Lớp đã đủ sĩ số tối đa");
    public static readonly Error NotFound = Error.NotFound("academic.class.not_found", "Không tìm thấy lớp học");
    public static readonly Error InvalidTeacher = Error.Validation("academic.class.invalid_teacher",
        "Giáo viên / trợ giảng không tồn tại, đã nghỉ việc hoặc không phải người dạy");
    public static readonly Error CourseNotAvailable = Error.Conflict("academic.course.not_available",
        "Khóa học đang tạm ngừng hoặc không bán ở chi nhánh này");
    public static readonly Error CancelConfirm = Error.Validation("academic.class.cancel_confirm", "Gõ đúng mã lớp để xác nhận hủy");

    public static Error InvalidCode(string prefix) =>
        Error.Validation("academic.class.invalid_code", $"Mã lớp phải bắt đầu bằng {prefix}, phần sau gồm chữ không dấu, số, dấu -");

    public static Error DuplicateCode(string code) => Error.Conflict("academic.class.duplicate_code", $"Mã {code} đã tồn tại");

    /// <param name="codes">Mã các lớp trùng GV / phòng</param>
    public static Error ScheduleConflict(IEnumerable<string> codes) =>
        Error.Conflict("academic.class.schedule_conflict", $"Trùng lịch giáo viên / phòng với lớp {string.Join(", ", codes)}");

    public static Error RoomTooSmall(int capacity) =>
        Error.Validation("academic.class.invalid_size", $"Sĩ số tối đa vượt sức chứa phòng ({capacity} chỗ)");

    public static Error BelowEnrolled(int enrolled) =>
        Error.Validation("academic.class.invalid_size", $"Sĩ số tối đa không được nhỏ hơn số học viên hiện có ({enrolled})");
}
