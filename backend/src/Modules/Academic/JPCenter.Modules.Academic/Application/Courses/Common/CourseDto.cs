using JPCenter.Modules.Academic.Domain.Courses;

namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>
/// Khóa học — khớp <c>Course</c> ở <c>frontend/src/features/academic/courses/types.ts</c> (+ lộ trình gắn, lớp đang mở).
/// Cấp độ trả đúng dạng frontend: <c>beginner</c>, <c>N5</c> … <c>N1</c>.
/// </summary>
public sealed record CourseDto(
    Guid Id,
    string Code,
    string Name,
    CourseCategory Category,
    string LevelFrom,
    string LevelTo,
    CourseFormat Format,
    TeacherType TeacherType,
    StudyMode Mode,
    int Sessions,
    int MinutesPerSession,
    int SessionsPerWeek,
    decimal Fee,
    int? MaxStudents,
    IReadOnlyList<Guid> BranchIds,
    string? Description,
    CourseStatus Status,
    IReadOnlyList<CourseCurriculumDto> Curricula,
    /// <summary>Mã các lớp đang mở của khóa (chờ khai giảng / đang học) — ⏳ có khi làm Lớp học</summary>
    IReadOnlyList<string> OpenClasses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    /// <summary>Chống sửa đè — gửi lại khi sửa</summary>
    uint RowVersion);

/// <summary>Lộ trình gắn với khóa</summary>
public sealed record CourseCurriculumDto(Guid CurriculumId, bool IsDefault, DateTimeOffset LinkedAt);
