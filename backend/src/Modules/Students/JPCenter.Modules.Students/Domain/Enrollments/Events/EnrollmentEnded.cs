using JPCenter.SharedKernel.Domain;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Enrollments.Events;

/// <summary>Ghi danh kết thúc (hoàn thành / chuyển / nghỉ) → Finance xử lý phần học phí còn lại.</summary>
public sealed record EnrollmentEnded(EnrollmentId EnrollmentId, StudentId StudentId, Guid ClassId, EnrollmentStatus Status) : IDomainEvent;
