using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Courses;

/// <summary>Lỗi khóa học (user story 09 — KH-xx)</summary>
public static class CourseErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.course.not_found", "Không tìm thấy khóa học");
    public static readonly Error NameRequired = Error.Validation("academic.course.name_required", "Chưa nhập tên khóa học");
    public static readonly Error InvalidCode =
        Error.Validation("academic.course.invalid_code", "Mã khóa 2–30 ký tự, chỉ gồm chữ không dấu, số và dấu gạch ngang");
    public static readonly Error DuplicateCode = Error.Conflict("academic.course.duplicate_code", "Mã khóa đã tồn tại");
    public static readonly Error InvalidSchedule =
        Error.Validation("academic.course.invalid_schedule", "Số buổi, phút / buổi, buổi / tuần phải lớn hơn 0");
    public static readonly Error InvalidLevels = Error.Validation("academic.course.invalid_levels", "Trình độ đầu ra không thấp hơn đầu vào");
    public static readonly Error MaxStudentsRequired = Error.Validation("academic.course.max_students_required", "Lớp nhóm cần sĩ số tối đa");
    public static readonly Error InvalidFee = Error.Validation("academic.course.invalid_fee", "Học phí phải lớn hơn 0");
    public static readonly Error BranchRequired = Error.Validation("academic.course.branch_required", "Chọn ít nhất 1 chi nhánh mở bán");
    public static readonly Error CodeLocked = Error.Conflict("academic.course.code_locked", "Khóa đã có lớp — không đổi được mã khóa");
    public static readonly Error FormatLocked = Error.Conflict("academic.course.format_locked",
        "Khóa đã có lớp — không đổi hình thức / loại giáo viên. Hãy nhân bản khóa để tạo biến thể mới");
    public static readonly Error NoDefaultCurriculum = Error.Conflict("academic.course.no_default_curriculum",
        "Khóa chưa có lộ trình mặc định đã phát hành — gắn lộ trình trước khi mở tuyển sinh");
    public static readonly Error NotActive = Error.Conflict("academic.course.not_active", "Khóa không ở trạng thái đang tuyển sinh");
    public static readonly Error DefaultCurriculum = Error.Conflict("academic.course.default_curriculum",
        "Đang là lộ trình mặc định — đặt lộ trình khác làm mặc định trước khi gỡ");
    public static readonly Error CurriculumNotLinked = Error.NotFound("academic.course.curriculum_not_linked", "Lộ trình chưa được gắn vào khóa");
    public static readonly Error DefaultNotPublished = Error.Conflict("academic.course.default_not_published",
        "Lộ trình mặc định phải là bản đã phát hành");

    public static Error CurriculumInUse(IEnumerable<string> classCodes) => Error.Conflict("academic.course.curriculum_in_use",
        $"Lớp {string.Join(", ", classCodes)} đang học theo lộ trình này — không thể gỡ");
}
