using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Id 1 lượt làm test</summary>
public readonly record struct PlacementAttemptId(Guid Value)
{
    public static PlacementAttemptId New() => new(IdGenerator.NewId());
}
