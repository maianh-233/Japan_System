using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.TeacherDocuments;

/// <summary>Id tài liệu GV tự soạn</summary>
public readonly record struct TeacherDocumentId(Guid Value)
{
    public static TeacherDocumentId New() => new(IdGenerator.NewId());
}
