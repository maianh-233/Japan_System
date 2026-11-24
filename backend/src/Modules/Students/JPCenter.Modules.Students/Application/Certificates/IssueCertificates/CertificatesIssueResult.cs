using JPCenter.Modules.Students.Application.Certificates.Common;

namespace JPCenter.Modules.Students.Application.Certificates.IssueCertificates;

/// <summary>1 HV trong lô "Cấp &amp; gửi": ghi danh + email nhận</summary>
public sealed record CertificateTarget(Guid EnrollmentId, string? Email);

/// <summary>Kết quả cấp hàng loạt: đã cấp (kèm gửi email được chưa) + bỏ qua (mã lỗi, lý do)</summary>
public sealed record CertificatesIssueResult(IReadOnlyList<CertificateSendResult> Issued, IReadOnlyList<CertificateSkipped> Skipped);

public sealed record CertificateSkipped(Guid EnrollmentId, string Code, string Message);
