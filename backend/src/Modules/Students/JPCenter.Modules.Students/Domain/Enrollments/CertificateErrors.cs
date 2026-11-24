using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>Lỗi chứng nhận hoàn thành khóa (HV-15)</summary>
public static class CertificateErrors
{
    public static readonly Error NotFound = Error.NotFound("students.certificate.not_found", "Ghi danh chưa được cấp chứng nhận");
    /// <summary>HV-15.E1</summary>
    public static readonly Error NotEligible = Error.Conflict("students.certificate.not_eligible",
        "Học viên chưa đủ điều kiện — cần ghi lý do cấp ngoại lệ");
    /// <summary>HV-15.E2</summary>
    public static readonly Error NotCompleted = Error.Conflict("students.certificate.not_completed", "Học viên chưa hoàn thành lớp");
    /// <summary>HV-15.E3</summary>
    public static readonly Error AlreadyIssued = Error.Conflict("students.certificate.already_issued", "Đã cấp chứng nhận cho ghi danh này — chỉ gửi lại");
    public static readonly Error EmailRequired = Error.Validation("students.certificate.email_required", "Nhập email nhận chứng nhận hợp lệ");
    /// <summary>Tra cứu công khai /verify/{code} — mã không có / chưa cấp</summary>
    public static readonly Error InvalidCode = Error.NotFound("students.certificate.invalid_code", "Không tìm thấy chứng nhận với mã này");
    public static readonly Error ExceptionTooLong = Error.Validation("students.certificate.exception_too_long", "Lý do cấp ngoại lệ tối đa 500 ký tự");
}
