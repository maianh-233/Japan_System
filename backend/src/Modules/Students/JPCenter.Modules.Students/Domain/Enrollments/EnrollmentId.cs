using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>Id ghi danh</summary>
public readonly record struct EnrollmentId(Guid Value)
{
    public static EnrollmentId New() => new(IdGenerator.NewId());
}
