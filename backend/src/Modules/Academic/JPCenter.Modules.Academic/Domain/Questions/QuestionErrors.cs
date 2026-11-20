using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Questions;

public static class QuestionErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.question.not_found", "Không tìm thấy câu hỏi");
    public static readonly Error InvalidStatus = Error.Conflict("academic.question.invalid_status", "Không chuyển được trạng thái câu hỏi");
    public static readonly Error InvalidLevel = Error.Validation("academic.question.invalid_level", "Câu hỏi phải gắn cấp độ N5–N1");
    public static readonly Error InvalidCategory = Error.Validation("academic.question.invalid_category", "Kỹ năng / dạng câu / độ khó không hợp lệ");
    public static readonly Error StemRequired = Error.Validation("academic.question.stem_required", "Chưa nhập đề bài");
    public static readonly Error Options = Error.Validation("academic.question.options", "Cần đúng 4 đáp án, không để trống và không trùng nhau");
    public static readonly Error AnswerInvalid = Error.Validation("academic.question.answer", "Đáp án đúng không hợp lệ");
    public static readonly Error StarMissing = Error.Validation("academic.question.star_missing", "Câu sắp xếp phải có ký tự ★ trong đề bài");
    public static readonly Error PassageRequired = Error.Validation("academic.question.passage_required", "Câu đọc hiểu cần đoạn văn");
    public static readonly Error AudioRequired = Error.Validation("academic.question.audio_required", "Câu nghe hiểu cần tải file nghe trước khi gửi duyệt");
    public static readonly Error AudioNotListening = Error.Validation("academic.question.audio_not_listening", "Chỉ câu nghe hiểu mới có file nghe");
    public static readonly Error AudioDuration = Error.Validation("academic.question.audio_duration", "Độ dài file nghe phải từ 1 giây đến 30 phút");
    public static readonly Error LessonInvalid =
        Error.Validation("academic.question.lesson_invalid", "Bài học phải thuộc lộ trình đã chọn, và lộ trình phải cùng cấp độ với câu hỏi");
    public static readonly Error SelfReview = Error.Forbidden("academic.question.self_review", "Người soạn không tự duyệt câu hỏi của mình");
    public static readonly Error NoteRequired = Error.Validation("academic.question.note_required", "Cần ghi lý do");
    public static readonly Error Retired = Error.Conflict("academic.question.retired", "Câu hỏi đã ngừng dùng — mở lại trước khi sửa");

    public static Error TooLong(string field, int max) => Error.Validation("academic.question.too_long", $"{field} tối đa {max} ký tự");
}
