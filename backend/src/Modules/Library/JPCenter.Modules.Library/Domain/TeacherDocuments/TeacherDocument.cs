using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.TeacherDocuments;

/// <summary>
/// Tài liệu GV tự soạn — chỉ gửi cho lớp mình dạy (khác thư viện trung tâm). Bảng: library.teacher_documents
/// (+ teacher_document_views)
/// </summary>
public sealed class TeacherDocument : AggregateRoot<TeacherDocumentId>, IAuditable, ISoftDeletable
{
    private readonly List<TeacherDocumentView> _views = [];

    private TeacherDocument() { }

    /// <summary>Hr.Employee</summary>
    public Guid TeacherId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string FileName { get; private set; } = null!;
    public TeacherDocKind Kind { get; private set; }
    public long? Size { get; private set; }
    /// <summary>Khóa file / link ngoài</summary>
    public string? Url { get; private set; }
    /// <summary>Lớp được xem (Academic.Class) — chỉ lớp GV đang dạy</summary>
    public List<Guid> ClassIds { get; private set; } = [];
    /// <summary>Gắn với buổi học (Academic.ClassSession)</summary>
    public Guid? SessionId { get; private set; }
    public IReadOnlyList<TeacherDocumentView> Views => _views;

    public static Result<TeacherDocument> Create(Guid teacherId, string title, string? description, string fileName, TeacherDocKind kind,
        long? size, string? url, List<Guid> classIds, Guid? sessionId)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("library.teacher_doc.title_required", "Chưa nhập tên tài liệu");
        return new TeacherDocument
        {
            Id = TeacherDocumentId.New(), TeacherId = teacherId, Title = title, Description = description, FileName = fileName, Kind = kind,
            Size = size, Url = url, ClassIds = classIds, SessionId = sessionId,
        };
    }

    public void ShareTo(List<Guid> classIds) => ClassIds = classIds;

    public void MarkViewed(Guid studentId, DateTimeOffset at)
    {
        if (_views.All(v => v.StudentId != studentId)) _views.Add(new TeacherDocumentView(studentId, at));
    }
}
