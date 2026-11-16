using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Id bài test xếp lớp</summary>
public readonly record struct PlacementTestId(Guid Value)
{
    public static PlacementTestId New() => new(IdGenerator.NewId());
}
