using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Lỗi kỳ thi / bài làm / kết quả thi (user story 12, ND-04 → ND-06)</summary>
public static class ExamErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.exam.not_found", "Không tìm thấy kỳ thi");
    public static readonly Error TitleRequired = Error.Validation("academic.exam.title_required", "Nhập tên kỳ thi");
    public static readonly Error InvalidDuration = Error.Validation("academic.exam.invalid_duration", "Thời lượng thi từ 10 đến 240 phút");
    public static readonly Error InvalidPassPercent = Error.Validation("academic.exam.invalid_pass", "% đạt từ 1 đến 100");
    /// <summary>ND-04.E1</summary>
    public static readonly Error Duplicate = Error.Conflict("academic.exam.duplicate", "Mốc thi này của lớp đã có kỳ thi");
    /// <summary>ND-04.E3</summary>
    public static readonly Error Sections = Error.Validation("academic.exam.sections",
        "Cần ít nhất 1 phần thi; mỗi phần có tên, ít nhất 1 câu, điểm mỗi câu từ trên 0 đến 100, không trùng câu giữa các phần");
    /// <summary>ND-04.E4</summary>
    public static readonly Error Published = Error.Conflict("academic.exam.published", "Kỳ thi đã phát hành — đề đã chốt");
    /// <summary>ND-04.E5</summary>
    public static readonly Error HasAttempts = Error.Conflict("academic.exam.has_attempts", "Đã có học viên làm bài");
    public static readonly Error Cancelled = Error.Conflict("academic.exam.cancelled", "Kỳ thi đã hủy");
    public static readonly Error NotPublished = Error.Conflict("academic.exam.not_published", "Kỳ thi chưa phát hành");
    /// <summary>ND-05.E1</summary>
    public static readonly Error NotOpen = Error.Conflict("academic.exam.not_open", "Chưa tới giờ thi hoặc đã hết giờ");
    /// <summary>ND-05.E2</summary>
    public static readonly Error AlreadySubmitted = Error.Conflict("academic.exam.already_submitted", "Bạn đã nộp bài thi này");
    /// <summary>ND-05.E5</summary>
    public static readonly Error PaperExam = Error.Conflict("academic.exam.paper", "Kỳ thi làm trên giấy tại lớp — không làm online");
    public static readonly Error QuestionNotInExam = Error.Validation("academic.exam.question_not_in_exam", "Câu hỏi không có trong đề của bạn");
    public static readonly Error InvalidChoice = Error.Validation("academic.exam.invalid_choice", "Đáp án không hợp lệ");
    /// <summary>ND-06.E1</summary>
    public static readonly Error InvalidScore = Error.Validation("academic.exam.invalid_score", "Điểm mỗi phần từ 0 đến điểm tối đa của phần");
    /// <summary>Công bố kết quả thi online trước khi hết giờ thi</summary>
    public static readonly Error NotFinished = Error.Conflict("academic.exam.not_finished", "Chưa hết giờ thi — chưa công bố được kết quả");

    /// <summary>ND-04.E2 — thiếu câu đã duyệt trong phạm vi bài, kèm số câu thiếu từng phần</summary>
    public static Error NotEnoughQuestions(IEnumerable<(string Section, int Missing)> missing) =>
        Error.Validation("academic.exam.not_enough_questions",
            $"Ngân hàng chưa đủ câu đã duyệt trong phạm vi bài: {string.Join(", ", missing.Select(m => $"{m.Section} thiếu {m.Missing}"))}");
}
