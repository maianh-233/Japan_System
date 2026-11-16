using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Classes;

public static class ClassErrors
{
    public static readonly Error TeacherCount = Error.Validation("academic.class.teacher_count", "Lớp cần 1 hoặc 2 giáo viên");
    public static readonly Error InvalidTime = Error.Validation("academic.class.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu");
    public static readonly Error InvalidWeekdays = Error.Validation("academic.class.invalid_weekdays", "Chọn thứ học từ Thứ 2 đến Chủ nhật");
    public static readonly Error InvalidSize = Error.Validation("academic.class.invalid_size", "Sĩ số tối đa phải ≥ tối thiểu ≥ 1");
    public static readonly Error InvalidDates = Error.Validation("academic.class.invalid_dates", "Ngày kết thúc phải sau ngày khai giảng");
    public static readonly Error NotEnrolling = Error.Conflict("academic.class.not_enrolling", "Chỉ sửa được khi lớp đang chờ khai giảng");
    public static readonly Error NotOngoing = Error.Conflict("academic.class.not_ongoing", "Lớp không ở trạng thái đang học");
    public static readonly Error Closed = Error.Conflict("academic.class.closed", "Lớp đã kết thúc hoặc đã hủy");
}
