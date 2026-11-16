using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Id ca được xếp</summary>
public readonly record struct ShiftAssignmentId(Guid Value)
{
    public static ShiftAssignmentId New() => new(IdGenerator.NewId());
}
