namespace JPCenter.Modules.Students.Contracts.Dtos;

/// <summary>Hồ sơ HV mới tạo từ lead (CRM-07 bước 1 — chính người liên hệ hoặc người khác)</summary>
/// <param name="Gender">"male" · "female" · "other" · null</param>
public sealed record NewStudentInfo(string FullName, string? Phone, string? Email, DateOnly? Dob, string? Gender, Guid BranchId, string? Note);

/// <summary>
/// Yêu cầu ghi danh từ lead: <paramref name="ExistingStudentId"/> (HV đã liên kết) HOẶC <paramref name="NewStudent"/>.
/// <paramref name="ClassId"/> null = chỉ tạo hồ sơ (CRM-07.E8). <paramref name="Amount"/> null = học phí lớp.
/// </summary>
public sealed record LeadEnrollmentRequest(
    Guid? ExistingStudentId,
    NewStudentInfo? NewStudent,
    Guid? ClassId,
    decimal? Amount,
    decimal Discount,
    DateOnly? DueDate,
    string LeadCode);

/// <param name="Created">true = hồ sơ HV vừa tạo</param>
/// <param name="Amount">Học phí ghi danh (null khi không chọn lớp)</param>
public sealed record LeadEnrollmentResult(
    Guid StudentId,
    string StudentCode,
    string StudentName,
    DateOnly? Dob,
    bool Created,
    Guid? EnrollmentId,
    Guid? ClassId,
    string? ClassCode,
    decimal? Amount,
    decimal Discount);
