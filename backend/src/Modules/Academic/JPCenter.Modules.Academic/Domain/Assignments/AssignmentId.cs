using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>Id bài tập</summary>
public readonly record struct AssignmentId(Guid Value)
{
    public static AssignmentId New() => new(IdGenerator.NewId());
}
