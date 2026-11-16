using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Id tài liệu giảng dạy</summary>
public readonly record struct MaterialId(Guid Value)
{
    public static MaterialId New() => new(IdGenerator.NewId());
}
