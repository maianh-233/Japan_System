using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Students;

/// <summary>
/// Hồ sơ học viên (bản ghi riêng, có thể tạo từ lead hoặc nhập Excel). Lớp đang học / trạng thái học SUY RA từ ghi danh (không lưu cột riêng).
/// Bảng: students.students. Không xóa — chỉ ẩn, và chỉ khi không còn ghi danh Đang học / Bảo lưu (HV-05).
/// </summary>
public sealed partial class Student : AggregateRoot<StudentId>, IBranchScoped, IAuditable, ISoftDeletable
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
    /// <summary>Ghi chú nội bộ — không trả về cổng GV / HV / PH</summary>
    public string? Note { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateOnly JoinedAt { get; private set; }
    public bool Hidden { get; private set; }
    public DateTimeOffset? HiddenAt { get; private set; }

    /// <summary>Mã HV theo số thứ tự: 1000 → HV1000</summary>
    public static string CodeFor(int number) => $"HV{number}";

    /// <param name="today">Hôm nay (giờ VN) — kiểm ngày sinh không ở tương lai</param>
    public static Result<Student> Create(string code, string fullName, Gender? gender, DateOnly? dob, string? phone, string? email,
        string? address, string? note, BranchId branchId, DateOnly joinedAt, DateOnly today)
    {
        var s = new Student { Id = StudentId.New(), Code = code, JoinedAt = joinedAt };
        var set = s.Update(fullName, gender, dob, phone, email, address, note, branchId, today);
        return set.IsFailure ? set.Error : s;
    }

    /// <summary>HV-03 — sửa hồ sơ (họ tên bắt buộc; ngày sinh không ở tương lai; SĐT 10 số bắt đầu bằng 0; email hợp lệ)</summary>
    public Result Update(string fullName, Gender? gender, DateOnly? dob, string? phone, string? email, string? address, string? note,
        BranchId branchId, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return StudentErrors.NameRequired;
        if (dob is { } d && d > today) return StudentErrors.InvalidDob;
        var cleanPhone = PhoneNumber.Normalize(phone);
        if (cleanPhone is not null && !PhoneNumber.IsValid(cleanPhone)) return StudentErrors.InvalidPhone;
        var cleanEmail = EmailAddress.Normalize(email);
        if (cleanEmail is not null && !EmailAddress.IsValid(cleanEmail)) return StudentErrors.InvalidEmail;

        FullName = CollapseSpaces(fullName);
        (Gender, Dob, Phone, Email) = (gender, dob, cleanPhone, cleanEmail);
        (Address, Note, BranchId) = (Clean(address), Clean(note), branchId);
        return Result.Success();
    }

    /// <summary>Tuổi tại ngày cho trước — dùng xác định người quyết định (HV dưới 18 → phụ huynh)</summary>
    public int? AgeAt(DateOnly date)
    {
        if (Dob is not { } dob) return null;
        var age = date.Year - dob.Year;
        return dob.AddYears(age) > date ? age - 1 : age;
    }

    /// <param name="activeClasses">Mã lớp còn ghi danh Đang học / Bảo lưu — rỗng mới ẩn được</param>
    public Result Hide(IReadOnlyCollection<string> activeClasses, DateTimeOffset at)
    {
        if (activeClasses.Count > 0) return StudentErrors.StillStudying(string.Join(", ", activeClasses));
        if (!Hidden) (Hidden, HiddenAt) = (true, at);
        return Result.Success();
    }

    /// <summary>Bỏ ẩn (tự gọi khi ghi danh HV đã ẩn — HV-06.E5)</summary>
    public void Unhide() => (Hidden, HiddenAt) = (false, null);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string CollapseSpaces(string s) => SpacesPattern().Replace(s.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesPattern();
}
