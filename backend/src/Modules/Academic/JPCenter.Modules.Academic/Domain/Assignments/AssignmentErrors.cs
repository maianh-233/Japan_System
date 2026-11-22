using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>Lỗi bài tập / bài nộp (user story 12, ND-01 → ND-03)</summary>
public static class AssignmentErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.assignment.not_found", "Không tìm thấy bài tập");
    public static readonly Error TitleRequired = Error.Validation("academic.assignment.title_required", "Nhập tên bài tập");
    /// <summary>ND-01.E1</summary>
    public static readonly Error InvalidDue = Error.Validation("academic.assignment.invalid_due", "Hạn nộp phải sau lúc giao");
    /// <summary>ND-01.E2</summary>
    public static readonly Error InvalidMaxScore = Error.Validation("academic.assignment.invalid_max_score", "Thang điểm từ trên 0 đến 100");
    /// <summary>ND-01.E3</summary>
    public static readonly Error QuizConfig = Error.Validation("academic.assignment.quiz_config",
        "Bài trắc nghiệm cần ít nhất 1 bài học và 1 kỹ năng, số câu 1–100, số lần làm 1–10, giới hạn 1–240 phút");
    /// <summary>ND-01.E6</summary>
    public static readonly Error HasSubmissions = Error.Conflict("academic.assignment.has_submissions",
        "Đã có học viên nộp bài — chỉ sửa được tiêu đề, mô tả, hạn nộp");

    public static readonly Error SubmissionNotFound = Error.NotFound("academic.submission.not_found", "Không tìm thấy bài nộp");
    /// <summary>ND-02.E1</summary>
    public static readonly Error SubmissionEmpty = Error.Validation("academic.submission.empty", "Đính kèm file hoặc nhập bài làm");
    public static readonly Error TooManyFiles = Error.Validation("academic.submission.too_many_files", "Nộp tối đa 5 file");
    /// <summary>ND-02.E3</summary>
    public static readonly Error NoAttemptsLeft = Error.Conflict("academic.submission.no_attempts_left", "Đã hết số lần làm bài");
    /// <summary>ND-02.E5</summary>
    public static readonly Error AlreadyGraded = Error.Conflict("academic.submission.already_graded", "Bài đã được chấm — không nộp lại được");
    /// <summary>ND-02 — bài trắc nghiệm làm qua quiz, bài nộp file nộp qua upload</summary>
    public static readonly Error WrongType = Error.Validation("academic.submission.wrong_type", "Sai cách nộp với loại bài tập");
    /// <summary>ND-03.E1</summary>
    public static readonly Error InvalidScore = Error.Validation("academic.submission.invalid_score", "Điểm phải trong thang điểm, bước 0,25");
}
