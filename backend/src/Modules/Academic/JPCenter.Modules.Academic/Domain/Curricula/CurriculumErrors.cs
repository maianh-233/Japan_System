using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Lỗi lộ trình (user story 09 — LT-xx)</summary>
public static class CurriculumErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.curriculum.not_found", "Không tìm thấy lộ trình");
    public static readonly Error NotDraft = Error.Conflict("academic.curriculum.not_draft", "Lộ trình đã phát hành — tạo phiên bản mới để sửa");
    public static readonly Error NotPublished = Error.Conflict("academic.curriculum.not_published", "Lộ trình chưa phát hành");
    public static readonly Error NotArchived = Error.Conflict("academic.curriculum.not_archived", "Lộ trình không ở trạng thái lưu trữ");
    public static readonly Error Archived = Error.Conflict("academic.curriculum.archived", "Lộ trình đã lưu trữ");
    public static readonly Error Empty = Error.Conflict("academic.curriculum.empty", "Lộ trình chưa có bài học");
    public static readonly Error NameRequired = Error.Validation("academic.curriculum.name_required", "Chưa nhập tên lộ trình");
    public static readonly Error InvalidCode =
        Error.Validation("academic.curriculum.invalid_code", "Mã lộ trình 2–30 ký tự, chỉ gồm chữ không dấu, số và dấu gạch ngang");
    public static readonly Error InvalidVersion = Error.Validation("academic.curriculum.invalid_version", "Phiên bản dạng v1.0");
    public static readonly Error EntryRequirementRequired = Error.Validation("academic.curriculum.entry_requirement_required",
        "Nêu điều kiện đầu vào để Giáo vụ chọn đúng lộ trình nâng cao khi mở lớp");
    public static readonly Error DuplicateVersion = Error.Conflict("academic.curriculum.duplicate_version", "Mã và phiên bản lộ trình đã tồn tại");
    public static readonly Error DraftExists = Error.Conflict("academic.curriculum.draft_exists",
        "Đã có bản nháp phiên bản mới từ bản này — mở bản nháp đó để sửa");
    public static readonly Error InUse = Error.Conflict("academic.curriculum.in_use", "Lộ trình đang được dùng — không xóa được");

    public static Error IsDefault(int courseCount) => Error.Conflict("academic.curriculum.is_default",
        $"Lộ trình đang là mặc định của {courseCount} khóa — đặt lộ trình khác làm mặc định ở trang Khóa học trước");

    public static readonly Error LessonNotFound = Error.NotFound("academic.curriculum.lesson_not_found", "Không tìm thấy bài học");
    public static readonly Error ChapterNotFound = Error.NotFound("academic.curriculum.chapter_not_found", "Không tìm thấy phần học");
    public static readonly Error MaterialNotFound = Error.NotFound("academic.material.not_found", "Không tìm thấy tài liệu");
    public static readonly Error ExamNotFound = Error.NotFound("academic.curriculum.exam_not_found", "Không tìm thấy mốc thi");
    public static readonly Error TitleRequired = Error.Validation("academic.curriculum.title_required", "Chưa nhập tên");
    public static readonly Error InvalidOrder = Error.Validation("academic.curriculum.invalid_order", "Danh sách thứ tự không khớp nội dung hiện tại");
    public static readonly Error InvalidLink = Error.Validation("academic.material.invalid_link", "Đường dẫn không hợp lệ (bắt đầu bằng https://)");
    public static readonly Error NotDriveLink = Error.Validation("academic.material.not_drive", "Không phải link Google Drive / Docs");
    public static readonly Error LinkTypeRequired = Error.Validation("academic.material.link_type", "Loại tài liệu link chỉ gồm Video, Google Drive, Liên kết");
}
