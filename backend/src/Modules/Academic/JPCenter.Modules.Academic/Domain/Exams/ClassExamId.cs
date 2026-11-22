using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

public readonly record struct ClassExamId(Guid Value)
{
    public static ClassExamId New() => new(IdGenerator.NewId());
}
