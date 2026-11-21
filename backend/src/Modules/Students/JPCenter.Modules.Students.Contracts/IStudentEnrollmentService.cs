using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Contracts;

/// <summary>
/// CRM-07 — chốt lead: Students tạo hồ sơ HV (hoặc dùng HV có sẵn) + ghi danh lớp trong 1 giao dịch của Students,
/// phát <c>StudentEnrolled</c> để Finance lập hóa đơn. Crm tự kiểm quyền / phạm vi lead trước khi gọi; không ghi bảng Students trực tiếp.
/// </summary>
public interface IStudentEnrollmentService
{
    Task<Result<LeadEnrollmentResult>> EnrollFromLeadAsync(LeadEnrollmentRequest request, CancellationToken ct);
}
