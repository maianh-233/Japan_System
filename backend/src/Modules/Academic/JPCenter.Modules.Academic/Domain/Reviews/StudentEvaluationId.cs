using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>Id nhận xét cuối khóa</summary>
public readonly record struct StudentEvaluationId(Guid Value)
{
    public static StudentEvaluationId New() => new(IdGenerator.NewId());
}
