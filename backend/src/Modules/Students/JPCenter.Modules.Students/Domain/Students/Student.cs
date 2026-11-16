using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Students;

/// <summary>
/// Hồ sơ học viên (bản ghi riêng, có thể tạo từ lead). Lớp đang học / trạng thái học suy ra từ ghi danh.
/// Bảng: students.students. Chỉ ẩn được khi không còn ghi danh Đang học / Bảo lưu (kiểm tra ở Application).
/// </summary>
public sealed class Student : AggregateRoot<StudentId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private Student() { }

    /// <summary>Mã hiển thị, vd HV1000</summary>
    public string Code { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public Gender? Gender { get; private set; }
    public DateOnly? Dob { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? Note { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateOnly JoinedAt { get; private set; }
    public bool Hidden { get; private set; }
    public DateTimeOffset? HiddenAt { get; private set; }

    public static Result<Student> Create(string code, string fullName, Gender? gender, DateOnly? dob, string? phone, string? email,
        string? address, BranchId branchId, DateOnly joinedAt)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return Error.Validation("students.student.name_required", "Chưa nhập họ tên học viên");
        return new Student
        {
            Id = StudentId.New(), Code = code, FullName = fullName.Trim(), Gender = gender, Dob = dob, Phone = phone, Email = email,
            Address = address, BranchId = branchId, JoinedAt = joinedAt,
        };
    }

    public void UpdateProfile(string fullName, Gender? gender, DateOnly? dob, string? phone, string? email, string? address, string? note) =>
        (FullName, Gender, Dob, Phone, Email, Address, Note) = (fullName, gender, dob, phone, email, address, note);

    /// <summary>Tuổi tại ngày cho trước — dùng xác định người quyết định (HV nhỏ → phụ huynh)</summary>
    public int? AgeAt(DateOnly date)
    {
        if (Dob is not { } dob) return null;
        var age = date.Year - dob.Year;
        return dob.AddYears(age) > date ? age - 1 : age;
    }

    /// <param name="hasActiveEnrollment">Còn ghi danh Đang học / Bảo lưu</param>
    public Result Hide(bool hasActiveEnrollment, DateTimeOffset at)
    {
        if (hasActiveEnrollment) return Error.Conflict("students.student.still_studying", "Học viên còn đang học hoặc bảo lưu, không ẩn được");
        (Hidden, HiddenAt) = (true, at);
        return Result.Success();
    }

    public void Unhide() => (Hidden, HiddenAt) = (false, null);
}
