using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Application.Curricula.Common;

/// <summary>
/// Lộ trình kèm toàn bộ nội dung — khớp <c>Curriculum</c> ở <c>frontend/src/features/academic/curriculum/types.ts</c>.
/// <c>Version</c> = phiên bản nội dung ("v1.0"); <c>RowVersion</c> = chống sửa đè.
/// </summary>
public sealed record CurriculumDto(
    Guid Id,
    string Code,
    string Name,
    string Level,
    CurriculumTier Tier,
    string? EntryRequirement,
    string Version,
    Guid? PreviousVersionId,
    string? Textbook,
    string? Description,
    IReadOnlyList<string> Tags,
    CurriculumStatus Status,
    IReadOnlyList<LessonDto> Lessons,
    IReadOnlyList<ExamDto> Exams,
    Guid AuthorId,
    /// <summary>Tên người soạn (Hr) — "" nếu không tìm thấy</summary>
    string Author,
    IReadOnlyList<CurriculumCourseDto> Courses,
    /// <summary>Số lớp học theo lộ trình — ⏳ có khi làm Lớp học</summary>
    int ClassCount,
    DateTimeOffset UpdatedAt,
    uint RowVersion);

public sealed record LessonDto(Guid Id, string Title, bool HasQuiz, IReadOnlyList<ChapterDto> Chapters);

public sealed record ChapterDto(Guid Id, string Title, IReadOnlyList<MaterialDto> Materials);

/// <summary>
/// Tài liệu. Dạng link: <c>Url</c> = đường dẫn gốc. Dạng file: <c>Url</c> = null — xin link tạm qua <c>…/materials/{id}/link</c>
/// (kho riêng tư, kiểm quyền mỗi lần mở).
/// </summary>
public sealed record MaterialDto(Guid Id, string Name, MaterialType Type, string? Url, bool IsFile, string? FileName, string? ContentType,
    long? Size, bool SharedWithStudents);

public sealed record ExamDto(Guid Id, ExamKind Kind, string Title, Guid AfterLessonId, string? Note);

/// <summary>Khóa đang gắn lộ trình này</summary>
public sealed record CurriculumCourseDto(Guid CourseId, string CourseCode, string CourseName, bool IsDefault);
