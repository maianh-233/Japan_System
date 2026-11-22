namespace JPCenter.Modules.Library.Domain.TeacherDocuments;

/// <summary>Nội dung tài liệu: file trong kho (Url = khóa) hoặc link ngoài (Kind = Link, Url = địa chỉ)</summary>
public sealed record TeacherDocFile(string Name, TeacherDocKind Kind, string Url, long? Size);
