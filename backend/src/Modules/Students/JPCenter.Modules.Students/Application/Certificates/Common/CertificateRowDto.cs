using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Domain.Enrollments;

namespace JPCenter.Modules.Students.Application.Certificates.Common;

/// <summary>
/// HV-15 bước 1 — 1 dòng trang Chứng nhận hoàn thành: HV × lớp đã kết thúc, checklist 4 điều kiện (giống frontend <c>certificateChecks</c>),
/// đủ điều kiện chưa, chứng nhận đã cấp (nếu có). <c>Email</c> = email hồ sơ HV (gợi ý ô "gửi tới").
/// </summary>
public sealed record CertificateRowDto(
    Guid EnrollmentId,
    Guid StudentId,
    string StudentCode,
    string StudentName,
    string? Email,
    Guid ClassId,
    string? ClassCode,
    string? ClassName,
    EnrollmentStatus Status,
    DateOnly? EndedAt,
    IReadOnlyList<CertificateCheck> Checks,
    bool Eligible,
    CertificateDto? Certificate);

/// <summary>Kết quả cấp / gửi lại: ghi danh (kèm chứng nhận) + email đã gửi được chưa (E4 — lỗi gửi vẫn giữ chứng nhận, cho gửi lại)</summary>
public sealed record CertificateSendResult(EnrollmentDto Enrollment, bool EmailSent);

/// <summary>Trang tra cứu công khai <c>/verify/{code}</c> — chỉ thông tin in trên chứng nhận</summary>
public sealed record CertificateVerificationDto(string Code, string StudentName, string? ClassCode, string? ClassName, string? Level, DateTimeOffset IssuedAt);
