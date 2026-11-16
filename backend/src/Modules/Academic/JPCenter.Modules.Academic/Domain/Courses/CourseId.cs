using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Courses;

/// <summary>Id khóa học</summary>
public readonly record struct CourseId(Guid Value)
{
    public static CourseId New() => new(IdGenerator.NewId());
}
