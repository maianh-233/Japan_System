using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Roles;

/// <summary>Id vai trò</summary>
public readonly record struct RoleId(Guid Value)
{
    public static RoleId New() => new(IdGenerator.NewId());
}
