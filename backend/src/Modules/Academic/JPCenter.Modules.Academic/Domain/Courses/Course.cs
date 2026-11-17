using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Courses;

/// <summary>
/// Khóa học = "sản phẩm" trung tâm bán. Lớp mở THEO khóa. Học phí: lớp nhóm = trọn khóa / HV; 1-1 = mỗi buổi.
/// Bảng: academic.courses (+ course_curricula). User story 09 — KH-01 → KH-06.
/// </summary>
public sealed partial class Course : AggregateRoot<CourseId>, IAuditable, ISoftDeletable
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

    /// <summary>Lộ trình mặc định cho lớp mở mới (null = chưa gắn lộ trình nào)</summary>
    public CurriculumId? DefaultCurriculumId => _curricula.FirstOrDefault(c => c.IsDefault)?.CurriculumId;

    public static Result<Course> Create(string code, string name, CourseCategory category, JlptLevel levelFrom, JlptLevel levelTo,
        CourseFormat format, TeacherType teacherType, StudyMode mode, int sessions, int minutesPerSession, int sessionsPerWeek,
        Money fee, int? maxStudents, List<BranchId> branchIds, string? description)
    {
        var course = new Course { Id = CourseId.New(), Status = CourseStatus.Draft };
        var set = course.Apply(code, name, category, levelFrom, levelTo, format, teacherType, mode, sessions, minutesPerSession,
            sessionsPerWeek, fee, maxStudents, branchIds, description);
        return set.IsFailure ? set.Error : course;
    }

    /// <summary>
    /// Sửa khóa (KH-04). <paramref name="hasClasses"/> = khóa đã có lớp → khóa mã (E1), hình thức + loại GV (E2).
    /// Học phí / số buổi đổi chỉ áp cho lớp mở mới — lớp đã chốt giá lúc mở.
    /// </summary>
    public Result Update(string code, string name, CourseCategory category, JlptLevel levelFrom, JlptLevel levelTo,
        CourseFormat format, TeacherType teacherType, StudyMode mode, int sessions, int minutesPerSession, int sessionsPerWeek,
        Money fee, int? maxStudents, List<BranchId> branchIds, string? description, bool hasClasses)
    {
        if (hasClasses && NormalizeCode(code) != Code) return CourseErrors.CodeLocked;
        if (hasClasses && (format != Format || teacherType != TeacherType)) return CourseErrors.FormatLocked;
        return Apply(code, name, category, levelFrom, levelTo, format, teacherType, mode, sessions, minutesPerSession, sessionsPerWeek,
            fee, maxStudents, branchIds, description);
    }

    /// <summary>Mở tuyển sinh (KH-05). Cần lộ trình mặc định đã phát hành — handler kiểm trạng thái lộ trình (aggregate khác).</summary>
    public Result Activate(bool defaultCurriculumPublished)
    {
        if (DefaultCurriculumId is null || !defaultCurriculumPublished) return CourseErrors.NoDefaultCurriculum;
        Status = CourseStatus.Active;
        return Result.Success();
    }

    /// <summary>Ngừng tuyển sinh (KH-05) — lớp đang mở vẫn học bình thường</summary>
    public Result Pause()
    {
        if (Status != CourseStatus.Active) return CourseErrors.NotActive;
        Status = CourseStatus.Paused;
        return Result.Success();
    }

    /// <summary>
    /// Gắn lộ trình (KH-06). Khóa chưa có lộ trình → lộ trình đầu tiên tự thành mặc định. Gắn lại lộ trình đã có → chỉ đổi mặc định nếu yêu cầu.
    /// </summary>
    public void LinkCurriculum(CurriculumId curriculumId, bool isDefault, DateTimeOffset at)
    {
        if (_curricula.Any(c => c.CurriculumId == curriculumId))
        {
            if (isDefault) SetDefaultCurriculum(curriculumId);
            return;
        }
        if (_curricula.Count == 0) isDefault = true;
        if (isDefault) foreach (var c in _curricula) c.UnsetDefault();
        _curricula.Add(new CourseCurriculum(curriculumId, isDefault, at));
    }

    /// <summary>Đổi lộ trình mặc định — lớp đã mở giữ lộ trình đã chốt</summary>
    public Result SetDefaultCurriculum(CurriculumId curriculumId)
    {
        var link = _curricula.FirstOrDefault(c => c.CurriculumId == curriculumId);
        if (link is null) return CourseErrors.CurriculumNotLinked;
        foreach (var c in _curricula) c.UnsetDefault();
        link.MarkDefault();
        return Result.Success();
    }

    /// <summary>Gỡ lộ trình (KH-06.E1): không gỡ được lộ trình đang mặc định. Lớp đang học theo — handler kiểm (E2).</summary>
    public Result UnlinkCurriculum(CurriculumId curriculumId)
    {
        var link = _curricula.FirstOrDefault(c => c.CurriculumId == curriculumId);
        if (link is null) return Result.Success();
        if (link.IsDefault) return CourseErrors.DefaultCurriculum;
        _curricula.Remove(link);
        return Result.Success();
    }

    /// <summary>Mã chuẩn: bỏ khoảng trắng, chữ hoa</summary>
    public static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    private Result Apply(string code, string name, CourseCategory category, JlptLevel levelFrom, JlptLevel levelTo,
        CourseFormat format, TeacherType teacherType, StudyMode mode, int sessions, int minutesPerSession, int sessionsPerWeek,
        Money fee, int? maxStudents, List<BranchId> branchIds, string? description)
    {
        code = NormalizeCode(code);
        if (!CodePattern().IsMatch(code)) return CourseErrors.InvalidCode;
        if (string.IsNullOrWhiteSpace(name)) return CourseErrors.NameRequired;
        if (sessions <= 0 || minutesPerSession <= 0 || sessionsPerWeek <= 0) return CourseErrors.InvalidSchedule;
        if (levelTo < levelFrom) return CourseErrors.InvalidLevels;
        if (format == CourseFormat.Group && maxStudents is null or <= 0) return CourseErrors.MaxStudentsRequired;
        if (fee.Amount <= 0) return CourseErrors.InvalidFee;
        if (branchIds.Count == 0) return CourseErrors.BranchRequired;

        Code = code;
        Name = name.Trim();
        Category = category;
        LevelFrom = levelFrom;
        LevelTo = levelTo;
        Format = format;
        TeacherType = teacherType;
        Mode = mode;
        Sessions = sessions;
        MinutesPerSession = minutesPerSession;
        SessionsPerWeek = sessionsPerWeek;
        Fee = fee;
        MaxStudents = format == CourseFormat.Group ? maxStudents : null;
        BranchIds = branchIds.Distinct().ToList();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return Result.Success();
    }

    [GeneratedRegex("^[A-Z0-9-]{2,30}$")]
    private static partial Regex CodePattern();
}
