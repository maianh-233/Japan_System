using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>
/// Bài làm thi ONLINE của 1 HV (ND-05): 1 lần duy nhất, đề riêng (đảo câu trong phần + đảo đáp án khi kỳ thi bật đảo đề), tự lưu câu đã chọn,
/// nộp / hết giờ → chấm từng phần ở server (đáp án không bao giờ gửi về cổng trước khi nộp). Hạn nộp = giờ kết thúc chung của kỳ thi.
/// Bảng: academic.exam_attempts, UQ (class_exam_id, student_id)
/// </summary>
public sealed class ExamAttempt : AggregateRoot<ExamAttemptId>, IAuditable
{
    /// <summary>Trễ mạng: vẫn nhận câu trả lời / nộp trong 1 phút sau giờ kết thúc (ND-02.E4, ND-05)</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    private ExamAttempt() { }

    public ClassExamId ClassExamId { get; private set; }
    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    /// <summary>Giờ kết thúc kỳ thi lúc bắt đầu làm</summary>
    public DateTimeOffset DeadlineAt { get; private set; }
    /// <summary>Đề riêng (jsonb): thứ tự câu + thứ tự đáp án hiển thị</summary>
    public List<AttemptQuestion> Questions { get; private set; } = [];
    /// <summary>Câu trả lời đã lưu (jsonb) — chỉ số đáp án gốc</summary>
    public List<ExamAnswer> Answers { get; private set; } = [];
    public DateTimeOffset? SubmittedAt { get; private set; }
    /// <summary>Hết giờ, hệ thống tự nộp các câu đã lưu (ND-05.E4)</summary>
    public bool AutoSubmitted { get; private set; }

    public bool IsSubmitted => SubmittedAt is not null;

    /// <summary>Đã quá hạn nộp (kể cả thời gian trễ mạng) — job / lần gọi sau tự nộp</summary>
    public bool IsExpired(DateTimeOffset now) => now > DeadlineAt + Grace;

    /// <summary>Bắt đầu làm: kỳ thi online đang mở (E1). Thứ tự đảo cố định theo HV (seed = kỳ thi + HV).</summary>
    public static Result<ExamAttempt> Start(ClassExam exam, Guid studentId, DateTimeOffset now, int optionCount = QuestionContent.OptionCount)
    {
        var open = exam.CheckOpen(now);
        if (open.IsFailure) return open.Error;
        var rng = new Random(HashCode.Combine(exam.Id.Value, studentId) & int.MaxValue);
        var identity = Enumerable.Range(0, optionCount).ToList();
        var questions = new List<AttemptQuestion>();
        for (var i = 0; i < exam.Sections.Count; i++)
        {
            var ids = exam.Sections[i].QuestionIds.ToList();
            if (exam.Shuffle) ids = ids.OrderBy(_ => rng.Next()).ToList();
            questions.AddRange(ids.Select(q => new AttemptQuestion(q, i, exam.Shuffle ? identity.OrderBy(_ => rng.Next()).ToList() : identity.ToList())));
        }
        return new ExamAttempt
        {
            Id = ExamAttemptId.New(), ClassExamId = exam.Id, StudentId = studentId, StartedAt = now, DeadlineAt = exam.EndsAt, Questions = questions,
        };
    }

    /// <summary>Lưu / đổi câu trả lời: <paramref name="position"/> = vị trí đáp án HIỂN THỊ (0-based); null = bỏ chọn</summary>
    public Result Answer(QuestionId questionId, int? position, DateTimeOffset now)
    {
        if (IsSubmitted) return ExamErrors.AlreadySubmitted;
        if (IsExpired(now)) return ExamErrors.NotOpen;
        if (Questions.FirstOrDefault(q => q.QuestionId == questionId) is not { } q) return ExamErrors.QuestionNotInExam;
        if (position is { } p && (p < 0 || p >= q.OptionOrder.Count)) return ExamErrors.InvalidChoice;
        var rest = Answers.Where(a => a.QuestionId != questionId);
        Answers = position is { } chosen ? [.. rest, new ExamAnswer(questionId, q.OptionOrder[chosen])] : [.. rest];
        return Result.Success();
    }

    /// <summary>
    /// Nộp bài (HV bấm nộp, hoặc hệ thống tự nộp khi quá hạn) → điểm từng phần theo đáp án đúng <paramref name="answerKey"/>
    /// (chỉ số đáp án gốc của mỗi câu). Câu bỏ trống = sai.
    /// </summary>
    public Result<List<ExamSectionScore>> Submit(ClassExam exam, IReadOnlyDictionary<QuestionId, int> answerKey, DateTimeOffset now)
    {
        if (IsSubmitted) return ExamErrors.AlreadySubmitted;
        (SubmittedAt, AutoSubmitted) = (IsExpired(now) ? DeadlineAt : now, IsExpired(now));
        var chosen = Answers.ToDictionary(a => a.QuestionId, a => a.Choice);
        return exam.Sections.Select(s =>
        {
            var correct = s.QuestionIds.Count(q => chosen.TryGetValue(q, out var c) && answerKey.TryGetValue(q, out var key) && c == key);
            return new ExamSectionScore(s.Name, correct * s.Points, s.Max);
        }).ToList();
    }

    /// <summary>Số phút đã làm (tới lúc nộp)</summary>
    public int DurationMinutes => SubmittedAt is { } at ? (int)Math.Ceiling((at - StartedAt).TotalMinutes) : 0;
}
