using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>Bài nộp của 1 HV cho 1 bài tập (chưa nộp = không có bản ghi). Bảng: academic.submissions, UQ (assignment_id, student_id)</summary>
public sealed class Submission : AggregateRoot<SubmissionId>, IAuditable
{
    private Submission() { }

    public AssignmentId AssignmentId { get; private set; }
    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    /// <summary>Bài Upload: tệp nộp (jsonb)</summary>
    public List<AttachedFile> Files { get; private set; } = [];
    /// <summary>Lời nhắn / bài làm dạng chữ</summary>
    public string? Content { get; private set; }
    /// <summary>Bài Quiz: kết quả lần cao nhất</summary>
    public int? QuizCorrect { get; private set; }
    public int? QuizTotal { get; private set; }
    public int QuizAttempts { get; private set; }
    public int? QuizDurationMinutes { get; private set; }
    public decimal? Score { get; private set; }
    /// <summary>Nhận xét của GV cho bài này</summary>
    public string? Comment { get; private set; }
    public DateTimeOffset? GradedAt { get; private set; }
    public Guid? GradedBy { get; private set; }

    public static Submission SubmitFiles(AssignmentId assignmentId, Guid studentId, List<AttachedFile> files, string? content, DateTimeOffset at) =>
        new() { Id = SubmissionId.New(), AssignmentId = assignmentId, StudentId = studentId, Files = files, Content = content, SubmittedAt = at };

    public static Submission SubmitQuiz(AssignmentId assignmentId, Guid studentId, int correct, int total, int durationMinutes, decimal maxScore,
        DateTimeOffset at)
    {
        var s = new Submission { Id = SubmissionId.New(), AssignmentId = assignmentId, StudentId = studentId, SubmittedAt = at };
        s.RecordQuizAttempt(correct, total, durationMinutes, maxScore, at);
        return s;
    }

    /// <summary>Làm lại quiz — giữ lần điểm cao nhất</summary>
    public Result RecordQuizAttempt(int correct, int total, int durationMinutes, decimal maxScore, DateTimeOffset at, int? maxAttempts = null)
    {
        if (maxAttempts is { } m && QuizAttempts >= m) return Error.Conflict("academic.submission.no_attempts_left", "Đã hết số lần làm bài");
        QuizAttempts++;
        if (QuizCorrect is null || correct > QuizCorrect)
        {
            (QuizCorrect, QuizTotal, QuizDurationMinutes, SubmittedAt) = (correct, total, durationMinutes, at);
            Score = total == 0 ? 0 : decimal.Round(maxScore * correct / total, 2);
            GradedAt = at;
        }
        return Result.Success();
    }

    public Result Grade(decimal score, decimal maxScore, string? comment, Guid by, DateTimeOffset at)
    {
        if (score < 0 || score > maxScore) return Error.Validation("academic.submission.invalid_score", "Điểm phải trong thang điểm");
        (Score, Comment, GradedBy, GradedAt) = (score, comment, by, at);
        return Result.Success();
    }
}
