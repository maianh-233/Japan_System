using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Classes;

/// <summary>Id lớp học</summary>
public readonly record struct ClassId(Guid Value)
{
    public static ClassId New() => new(IdGenerator.NewId());
}
