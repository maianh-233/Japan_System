using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>Id tài liệu thư viện</summary>
public readonly record struct LibraryDocumentId(Guid Value)
{
    public static LibraryDocumentId New() => new(IdGenerator.NewId());
}
