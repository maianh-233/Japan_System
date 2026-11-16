using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Students;

/// <summary>Id học viên</summary>
public readonly record struct StudentId(Guid Value)
{
    public static StudentId New() => new(IdGenerator.NewId());
}
