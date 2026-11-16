using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Courses;

/// <summary>Khóa ↔ lộ trình (nhiều–nhiều). Bảng: academic.course_curricula, khóa (course_id, curriculum_id)</summary>
public sealed class CourseCurriculum
{
    private CourseCurriculum() { }

    internal CourseCurriculum(CurriculumId curriculumId, bool isDefault, DateTimeOffset linkedAt) =>
        (CurriculumId, IsDefault, LinkedAt) = (curriculumId, isDefault, linkedAt);

    public CurriculumId CurriculumId { get; private set; }
    /// <summary>Lộ trình mặc định cho lớp mở mới (thường bậc Chuẩn)</summary>
    public bool IsDefault { get; private set; }
    public DateTimeOffset LinkedAt { get; private set; }

    internal void UnsetDefault() => IsDefault = false;
}
