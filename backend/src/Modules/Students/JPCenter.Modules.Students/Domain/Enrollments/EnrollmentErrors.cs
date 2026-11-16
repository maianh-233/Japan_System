using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

public static class EnrollmentErrors
{
    public static readonly Error NotStudying = Error.Conflict("students.enrollment.not_studying", "Học viên không ở trạng thái đang học");
    public static readonly Error AlreadyEnded = Error.Conflict("students.enrollment.already_ended", "Ghi danh đã kết thúc");
}
