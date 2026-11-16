using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Id lộ trình</summary>
public readonly record struct CurriculumId(Guid Value)
{
    public static CurriculumId New() => new(IdGenerator.NewId());
}
