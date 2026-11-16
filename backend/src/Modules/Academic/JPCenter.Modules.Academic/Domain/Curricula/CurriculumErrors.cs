using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

public static class CurriculumErrors
{
    public static readonly Error NotDraft = Error.Conflict("academic.curriculum.not_draft", "Lộ trình đã phát hành — tạo phiên bản mới để sửa");
    public static readonly Error LessonNotFound = Error.NotFound("academic.curriculum.lesson_not_found", "Không tìm thấy bài học");
    public static readonly Error ChapterNotFound = Error.NotFound("academic.curriculum.chapter_not_found", "Không tìm thấy phần học");
}
