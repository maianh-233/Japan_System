using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Trash;

/// <summary>Id mục trong thùng rác</summary>
public readonly record struct TrashItemId(Guid Value)
{
    public static TrashItemId New() => new(IdGenerator.NewId());
}
