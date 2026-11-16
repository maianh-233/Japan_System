namespace JPCenter.Modules.Library.Domain.TeacherDocuments;

/// <summary>HV đã xem tài liệu GV. Bảng: library.teacher_document_views, khóa (teacher_document_id, student_id)</summary>
public sealed class TeacherDocumentView
{
    private TeacherDocumentView() { }

    internal TeacherDocumentView(Guid studentId, DateTimeOffset viewedAt) => (StudentId, ViewedAt) = (studentId, viewedAt);

    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public DateTimeOffset ViewedAt { get; private set; }
}
