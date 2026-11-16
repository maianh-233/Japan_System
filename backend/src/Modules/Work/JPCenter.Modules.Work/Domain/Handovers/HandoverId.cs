using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>Id bàn giao ca</summary>
public readonly record struct HandoverId(Guid Value)
{
    public static HandoverId New() => new(IdGenerator.NewId());
}
