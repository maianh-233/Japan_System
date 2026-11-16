using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

public static class JlptErrors
{
    public static readonly Error InvalidStatus = Error.Conflict("students.jlpt.invalid_status", "Không chuyển được trạng thái đăng ký thi");
}
