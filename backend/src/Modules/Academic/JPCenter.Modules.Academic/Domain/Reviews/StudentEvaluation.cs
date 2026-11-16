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

    public static Result<StudentEvaluation> Create(ClassId classId, Guid studentId, Guid teacherId) =>
        new StudentEvaluation { Id = StudentEvaluationId.New(), ClassId = classId, StudentId = studentId, TeacherId = teacherId, Recommendation = EvalRecommendation.NextLevel };

    public Result Update(int vocab, int grammar, int reading, int listening, int speaking, int attitude, string? strengths, string? weaknesses,
        EvalRecommendation recommendation, string? advice)
    {
        int[] stars = [vocab, grammar, reading, listening, speaking, attitude];
        if (stars.Any(s => s is < 1 or > 5)) return Error.Validation("academic.evaluation.invalid_rating", "Đánh giá từ 1 đến 5 sao");
        (Vocab, Grammar, Reading, Listening, Speaking, Attitude) = (vocab, grammar, reading, listening, speaking, attitude);
        (Strengths, Weaknesses, Recommendation, Advice) = (strengths, weaknesses, recommendation, advice);
        return Result.Success();
    }
}
