using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>Id lượt xem tài liệu</summary>
public readonly record struct DocumentViewId(Guid Value)
{
    public static DocumentViewId New() => new(IdGenerator.NewId());
}
