using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Id phần trong bài</summary>
public readonly record struct ChapterId(Guid Value)
{
    public static ChapterId New() => new(IdGenerator.NewId());
}
