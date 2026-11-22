using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>
/// Bài nộp của 1 HV cho 1 bài tập (chưa nộp = không có bản ghi) — ND-02, ND-03. Upload: nộp lại được khi chưa chấm.
/// Quiz: giữ lần điểm cao nhất, giới hạn số lần. Bảng: academic.submissions, UQ (assignment_id, student_id)
/// </summary>
public sealed class Submission : AggregateRoot<SubmissionId>, IAuditable
{
    public const int MaxFiles = 5;
    /// <summary>Bước chấm điểm (ND-03)</summary>
    public const decimal ScoreStep = 0.25m;

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
    /// <summary>Người chấm (Hr.Employee); null = hệ thống tự chấm quiz</summary>
    public Guid? GradedBy { get; private set; }

    /// <summary>Đã được GV chấm (quiz tự chấm không tính — vẫn làm lại được)</summary>
    public bool IsGradedByTeacher => GradedBy is not null;

    public static Result<Submission> SubmitFiles(AssignmentId assignmentId, Guid studentId, List<AttachedFile> files, string? content,
        DateTimeOffset at)
    {
        var s = new Submission { Id = SubmissionId.New(), AssignmentId = assignmentId, StudentId = studentId };
        var set = s.SetWork(files, content, at);
        return set.IsFailure ? set.Error : s;
    }

    /// <summary>ND-02 — nộp lại bài upload (thay toàn bộ file + bài làm) khi GV chưa chấm (E5)</summary>
    public Result Resubmit(List<AttachedFile> files, string? content, DateTimeOffset at) =>
        IsGradedByTeacher ? AssignmentErrors.AlreadyGraded : SetWork(files, content, at);

    public static Result<Submission> SubmitQuiz(AssignmentId assignmentId, Guid studentId, int correct, int total, int durationMinutes,
        decimal maxScore, int maxAttempts, DateTimeOffset at)
    {
        var s = new Submission { Id = SubmissionId.New(), AssignmentId = assignmentId, StudentId = studentId, SubmittedAt = at };
        var r = s.RecordQuizAttempt(correct, total, durationMinutes, maxScore, maxAttempts, at);
        return r.IsFailure ? r.Error : s;
    }

    /// <summary>Số lần làm quiz còn lại</summary>
    public int AttemptsLeft(int maxAttempts) => Math.Max(0, maxAttempts - QuizAttempts);

    /// <summary>1 lần làm quiz — giữ lần điểm cao nhất (E3: hết số lần → chặn). GV đã chấm đè thì không ghi đè điểm GV.</summary>
    public Result RecordQuizAttempt(int correct, int total, int durationMinutes, decimal maxScore, int maxAttempts, DateTimeOffset at)
    {
        if (QuizAttempts >= maxAttempts) return AssignmentErrors.NoAttemptsLeft;
        if (total < 0 || correct < 0 || correct > total) return AssignmentErrors.InvalidScore;
        QuizAttempts++;
        if (QuizCorrect is null || correct > QuizCorrect)
        {
            (QuizCorrect, QuizTotal, QuizDurationMinutes, SubmittedAt) = (correct, total, Math.Max(0, durationMinutes), at);
            if (!IsGradedByTeacher)
            {
                Score = total == 0 ? 0 : RoundToStep(maxScore * correct / total);
                GradedAt = at;
            }
        }
        return Result.Success();
    }

    /// <summary>ND-03 — GV chấm / sửa điểm (0 → thang điểm, bước 0,25) + nhận xét; quiz: ghi đè điểm tự chấm</summary>
    public Result Grade(decimal score, decimal maxScore, string? comment, Guid by, DateTimeOffset at)
    {
        if (score < 0 || score > maxScore || score % ScoreStep != 0) return AssignmentErrors.InvalidScore;
        (Score, Comment, GradedBy, GradedAt) = (score, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), by, at);
        return Result.Success();
    }

    private Result SetWork(List<AttachedFile> files, string? content, DateTimeOffset at)
    {
        var text = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        if (files.Count == 0 && text is null) return AssignmentErrors.SubmissionEmpty;
        if (files.Count > MaxFiles) return AssignmentErrors.TooManyFiles;
        (Files, Content, SubmittedAt) = (files, text, at);
        return Result.Success();
    }

    /// <summary>Làm tròn xuống bước 0,25 — điểm tự chấm không vượt thang</summary>
    private static decimal RoundToStep(decimal value) => decimal.Floor(value / ScoreStep) * ScoreStep;
}
