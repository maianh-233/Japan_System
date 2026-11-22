using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.TeacherDocuments;

/// <summary>
/// Tài liệu GV tự soạn, gửi cho lớp mình dạy (khác thư viện trung tâm) — user story 12, ND-10: file (khóa kho file) hoặc link, gắn buổi học.
/// Đổi file / link → lượt xem về 0 (HV cần xem bản mới). Lớp phải là lớp GV dạy (Application kiểm qua Academic.Contracts — E1).
/// Bảng: library.teacher_documents (+ teacher_document_views)
/// </summary>
public sealed class TeacherDocument : AggregateRoot<TeacherDocumentId>, IAuditable, ISoftDeletable
{
    public const int MaxTitle = 200;

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
    public string Url { get; private set; } = null!;
    /// <summary>Lớp được xem (Academic.Class) — chỉ lớp GV đang dạy</summary>
    public List<Guid> ClassIds { get; private set; } = [];
    /// <summary>Gắn với buổi học (Academic.ClassSession)</summary>
    public Guid? SessionId { get; private set; }
    public IReadOnlyList<TeacherDocumentView> Views => _views;

    /// <summary>Tài liệu là file trong kho (không phải link) — xóa file khi dọn Thùng rác</summary>
    public bool IsStoredFile => Kind != TeacherDocKind.Link;

    public static Result<TeacherDocument> Create(Guid teacherId, string? title, string? description, TeacherDocFile? file, List<Guid> classIds,
        Guid? sessionId)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.Url)) return TeacherDocumentErrors.ContentRequired;
        var doc = new TeacherDocument { Id = TeacherDocumentId.New(), TeacherId = teacherId };
        doc.SetFile(file);
        var set = doc.Update(teacherId, title, description, null, classIds, sessionId);
        return set.IsFailure ? set.Error : doc;
    }

    /// <summary>Sửa (chỉ người soạn); <paramref name="newFile"/> khác null = thay file / link → xóa lượt xem</summary>
    public Result Update(Guid by, string? title, string? description, TeacherDocFile? newFile, List<Guid> classIds, Guid? sessionId)
    {
        if (by != TeacherId) return TeacherDocumentErrors.NotOwner;
        if (string.IsNullOrWhiteSpace(title)) return TeacherDocumentErrors.TitleRequired;
        if (title.Trim().Length > MaxTitle) return TeacherDocumentErrors.TitleTooLong;
        if (classIds.Count == 0) return TeacherDocumentErrors.ClassRequired;
        if (newFile is not null && string.IsNullOrWhiteSpace(newFile.Url)) return TeacherDocumentErrors.ContentRequired;
        (Title, Description, ClassIds, SessionId) = (title.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            classIds.Distinct().ToList(), sessionId);
        if (newFile is not null)
        {
            SetFile(newFile);
            _views.Clear();
        }
        return Result.Success();
    }

    /// <summary>ND-10.E2 — HV mở tài liệu: ghi lần xem đầu; trả false nếu đã xem trước đó</summary>
    public bool MarkViewed(Guid studentId, DateTimeOffset at)
    {
        if (_views.Any(v => v.StudentId == studentId)) return false;
        _views.Add(new TeacherDocumentView(studentId, at));
        return true;
    }

    private void SetFile(TeacherDocFile file) => (FileName, Kind, Url, Size) = (file.Name.Trim(), file.Kind, file.Url.Trim(), file.Size);
}
