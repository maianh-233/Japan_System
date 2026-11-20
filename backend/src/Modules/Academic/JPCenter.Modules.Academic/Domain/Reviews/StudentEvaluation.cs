using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>
/// Nhận xét CUỐI KHÓA của GV về 1 HV — căn cứ để Sale tư vấn khóa tiếp theo. KHÔNG hiện cho HV / PH.
/// Mỗi HV / lớp 1 bản, GV sửa được. Bảng: academic.student_evaluations, UQ (class_id, student_id)
/// </summary>
public sealed class StudentEvaluation : AggregateRoot<StudentEvaluationId>, IAuditable
{
    private StudentEvaluation() { }

    public ClassId ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid TeacherId { get; private set; }
    /// <summary>1–5 sao từng kỹ năng</summary>
    public int Vocab { get; private set; }
    public int Grammar { get; private set; }
    public int Reading { get; private set; }
    public int Listening { get; private set; }
    public int Speaking { get; private set; }
    /// <summary>Thái độ, chuyên cần 1–5</summary>
    public int Attitude { get; private set; }
    public string? Strengths { get; private set; }
    public string? Weaknesses { get; private set; }
    public EvalRecommendation Recommendation { get; private set; }
    /// <summary>Ghi chú cho trung tâm / Sale khi tư vấn</summary>
    public string? Advice { get; private set; }

    /// <summary>GV sửa được tới khi lớp kết thúc + N ngày (DT-19)</summary>
    public const int EditableDaysAfterClassEnd = 14;

    public static readonly Error InvalidRating = Error.Validation("academic.evaluation.invalid_rating", "Đánh giá từ 1 đến 5 sao");
    public static readonly Error Locked = Error.Conflict("academic.evaluation.locked", "Đã quá 14 ngày sau khi lớp kết thúc — không sửa được nhận xét");

    /// <summary>Còn sửa được không: lớp chưa kết thúc, hoặc kết thúc chưa quá 14 ngày</summary>
    public static bool IsEditable(DateOnly? classEndedOn, DateOnly today) =>
        classEndedOn is not { } end || today <= end.AddDays(EditableDaysAfterClassEnd);

    /// <summary>Tạo nhận xét (UQ lớp × HV — handler kiểm đã có thì sửa)</summary>
    public static Result<StudentEvaluation> Create(ClassId classId, Guid studentId, Guid teacherId, EvaluationScores scores, string? strengths,
        string? weaknesses, EvalRecommendation recommendation, string? advice, DateOnly? classEndedOn, DateOnly today)
    {
        var e = new StudentEvaluation { Id = StudentEvaluationId.New(), ClassId = classId, StudentId = studentId, TeacherId = teacherId };
        var r = e.Update(teacherId, scores, strengths, weaknesses, recommendation, advice, classEndedOn, today);
        return r.IsSuccess ? e : r.Error;
    }

    public Result Update(Guid teacherId, EvaluationScores scores, string? strengths, string? weaknesses, EvalRecommendation recommendation,
        string? advice, DateOnly? classEndedOn, DateOnly today)
    {
        if (!IsEditable(classEndedOn, today)) return Locked;
        if (!scores.IsValid) return InvalidRating;
        (Vocab, Grammar, Reading, Listening, Speaking, Attitude) =
            (scores.Vocab, scores.Grammar, scores.Reading, scores.Listening, scores.Speaking, scores.Attitude);
        (Strengths, Weaknesses, Recommendation, Advice, TeacherId) = (Clean(strengths), Clean(weaknesses), recommendation, Clean(advice), teacherId);
        return Result.Success();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
