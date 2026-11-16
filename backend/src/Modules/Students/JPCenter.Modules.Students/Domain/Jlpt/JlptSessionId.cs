using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Id kỳ thi JLPT</summary>
public readonly record struct JlptSessionId(Guid Value)
{
    public static JlptSessionId New() => new(IdGenerator.NewId());
}
