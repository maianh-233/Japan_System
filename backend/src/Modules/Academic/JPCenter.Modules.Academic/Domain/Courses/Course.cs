using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Courses;

/// <summary>
/// Khóa học = "sản phẩm" trung tâm bán. Lớp mở THEO khóa. Học phí: lớp nhóm = trọn khóa / HV; 1-1 = mỗi buổi.
/// Bảng: academic.courses (+ course_curricula)
/// </summary>
public sealed class Course : AggregateRoot<CourseId>, IAuditable, ISoftDeletable
{
    private readonly List<CourseCurriculum> _curricula = [];

    private Course() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public CourseCategory Category { get; private set; }
    /// <summary>Trình độ đầu vào</summary>
    public JlptLevel LevelFrom { get; private set; }
    /// <summary>Trình độ đầu ra</summary>
    public JlptLevel LevelTo { get; private set; }
    public CourseFormat Format { get; private set; }
    public TeacherType TeacherType { get; private set; }
    public StudyMode Mode { get; private set; }
    /// <summary>Tổng số buổi</summary>
    public int Sessions { get; private set; }
    public int MinutesPerSession { get; private set; }
    public int SessionsPerWeek { get; private set; }
    /// <summary>Lớp nhóm: giá trọn khóa. 1 kèm 1: giá mỗi buổi</summary>
    public Money Fee { get; private set; }
    /// <summary>Sĩ số tối đa (chỉ lớp nhóm)</summary>
    public int? MaxStudents { get; private set; }
    /// <summary>Chi nhánh được bán khóa này</summary>
    public List<BranchId> BranchIds { get; private set; } = [];
    public string? Description { get; private set; }
    public CourseStatus Status { get; private set; }
    public IReadOnlyList<CourseCurriculum> Curricula => _curricula;

    public static Result<Course> Create(string code, string name, CourseCategory category, JlptLevel levelFrom, JlptLevel levelTo,
        CourseFormat format, TeacherType teacherType, StudyMode mode, int sessions, int minutesPerSession, int sessionsPerWeek,
        Money fee, int? maxStudents, List<BranchId> branchIds, string? description)
    {
        if (sessions <= 0 || minutesPerSession <= 0 || sessionsPerWeek <= 0)
            return Error.Validation("academic.course.invalid_schedule", "Số buổi, phút / buổi, buổi / tuần phải lớn hơn 0");
        if (levelTo < levelFrom) return Error.Validation("academic.course.invalid_levels", "Trình độ đầu ra không thấp hơn đầu vào");
        if (format == CourseFormat.Group && maxStudents is null or <= 0)
            return Error.Validation("academic.course.max_students_required", "Lớp nhóm cần sĩ số tối đa");
        return new Course
        {
            Id = CourseId.New(), Code = code, Name = name, Category = category, LevelFrom = levelFrom, LevelTo = levelTo, Format = format,
            TeacherType = teacherType, Mode = mode, Sessions = sessions, MinutesPerSession = minutesPerSession, SessionsPerWeek = sessionsPerWeek,
            Fee = fee, MaxStudents = format == CourseFormat.Group ? maxStudents : null, BranchIds = branchIds, Description = description,
            Status = CourseStatus.Draft,
        };
    }

    /// <summary>Gắn lộ trình. Mỗi khóa có đúng 1 lộ trình mặc định — gắn mặc định mới thì bỏ mặc định cũ.</summary>
    public void LinkCurriculum(CurriculumId curriculumId, bool isDefault, DateTimeOffset at)
    {
        _curricula.RemoveAll(c => c.CurriculumId == curriculumId);
        if (isDefault || _curricula.Count == 0)
        {
            foreach (var c in _curricula) c.UnsetDefault();
            isDefault = true;
        }
        _curricula.Add(new CourseCurriculum(curriculumId, isDefault, at));
    }

    public Result UnlinkCurriculum(CurriculumId curriculumId)
    {
        var link = _curricula.FirstOrDefault(c => c.CurriculumId == curriculumId);
        if (link is null) return Result.Success();
        if (link.IsDefault && _curricula.Count > 1)
            return Error.Conflict("academic.course.default_curriculum", "Chọn lộ trình mặc định khác trước khi gỡ lộ trình này");
        _curricula.Remove(link);
        return Result.Success();
    }

    public void ChangeFee(Money fee) => Fee = fee;
    public void Activate() => Status = CourseStatus.Active;
    public void Pause() => Status = CourseStatus.Paused;
}
