using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Enrollments.Events;

/// <summary>Học viên được ghi danh → Finance tạo hóa đơn, Academic cập nhật sĩ số, System cấp tài khoản cổng.</summary>
public sealed record StudentEnrolled(EnrollmentId EnrollmentId, StudentId StudentId, Guid ClassId, BranchId BranchId) : IDomainEvent;
