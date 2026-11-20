using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Students;

public static class StudentErrors
{
    public static readonly Error NotFound = Error.NotFound("students.student.not_found", "Không tìm thấy học viên");
    public static readonly Error NameRequired = Error.Validation("students.student.name_required", "Chưa nhập họ tên học viên");
    public static readonly Error InvalidDob = Error.Validation("students.student.invalid_dob", "Ngày sinh không được ở tương lai");
    public static readonly Error InvalidPhone = Error.Validation("students.student.invalid_phone", "Số điện thoại gồm 10 số, bắt đầu bằng 0");
    public static readonly Error InvalidEmail = Error.Validation("students.student.invalid_email", "Email không hợp lệ");

    public static Error StillStudying(string where) =>
        Error.Conflict("students.student.still_studying", $"Đang trong lớp {where} — chỉ ẩn được khi học viên không còn học lớp nào");
}
