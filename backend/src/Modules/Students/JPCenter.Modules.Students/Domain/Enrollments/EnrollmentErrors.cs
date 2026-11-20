using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

public static class EnrollmentErrors
{
    public static readonly Error NotFound = Error.NotFound("students.enrollment.not_found", "Không tìm thấy ghi danh");
    public static readonly Error NotStudying = Error.Conflict("students.enrollment.not_studying", "Học viên không ở trạng thái đang học");
    public static readonly Error NotReserved = Error.Conflict("students.enrollment.not_reserved", "Ghi danh không ở trạng thái bảo lưu");
    public static readonly Error AlreadyEnded = Error.Conflict("students.enrollment.already_ended", "Ghi danh đã kết thúc");
    public static readonly Error Duplicate = Error.Conflict("students.enrollment.duplicate", "Học viên đã có trong lớp này");
    public static readonly Error SameClass = Error.Validation("students.enrollment.same_class", "Lớp chuyển đến phải khác lớp hiện tại");
    public static readonly Error ReasonRequired = Error.Validation("students.enrollment.reason_required", "Cần ghi lý do");
    public static readonly Error InvalidReserveUntil = Error.Validation("students.enrollment.invalid_reserve_until",
        $"Ngày bảo lưu đến phải sau hôm nay và không quá {Enrollment.MaxReserveMonths} tháng");
}
