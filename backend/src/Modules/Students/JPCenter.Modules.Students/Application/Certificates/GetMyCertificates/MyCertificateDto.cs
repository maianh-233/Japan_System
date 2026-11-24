using JPCenter.Modules.Students.Domain.Enrollments;

namespace JPCenter.Modules.Students.Application.Certificates.GetMyCertificates;

/// <summary>
/// HV-15.E5 — 1 lớp ở cổng HV / PH: checklist điều kiện (lớp đang học: điều kiện "đã hoàn thành lớp" chưa đạt) + chứng nhận đã cấp
/// (không có người gửi / lý do ngoại lệ — nội bộ)
/// </summary>
public sealed record MyCertificateDto(
    Guid EnrollmentId,
    Guid StudentId,
    Guid ClassId,
    EnrollmentStatus Status,
    DateOnly? EndedAt,
    IReadOnlyList<CertificateCheck> Checks,
    bool Eligible,
    MyCertificateInfo? Certificate);

public sealed record MyCertificateInfo(string Code, DateTimeOffset IssuedAt, string SentTo);
