using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>
/// HV đánh giá GV (ND-08 — khảo sát giữa / cuối kỳ trên cổng HV, mỗi HV × GV × đợt 1 lần), ẩn danh được (GV không thấy tên).
/// Bảng: academic.teacher_reviews, UQ (class_id, teacher_id, student_id, period)
/// </summary>
public sealed class TeacherReview : AggregateRoot<TeacherReviewId>
{
    public const int MaxComment = 2000;

    private TeacherReview() { }

    public ClassId ClassId { get; private set; }
    public Guid TeacherId { get; private set; }
    public Guid StudentId { get; private set; }
    public ReviewPeriod Period { get; private set; }
    /// <summary>Điểm tổng 1–5 sao</summary>
    public int Overall { get; private set; }
    /// <summary>Tiêu chí 1–5: phương pháp, dễ hiểu, nhiệt tình, đúng giờ</summary>
    public int Method { get; private set; }
    public int Clarity { get; private set; }
    public int Enthusiasm { get; private set; }
    public int Punctuality { get; private set; }
    public string? Comment { get; private set; }
    /// <summary>Ẩn tên HV với GV</summary>
    public bool Anonymous { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<TeacherReview> Create(ClassId classId, Guid teacherId, Guid studentId, ReviewPeriod period, int overall, int method,
        int clarity, int enthusiasm, int punctuality, string? comment, bool anonymous, DateTimeOffset at)
    {
        int[] stars = [overall, method, clarity, enthusiasm, punctuality];
        if (stars.Any(s => s is < 1 or > 5)) return TeacherReviewErrors.InvalidRating;
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (comment is { Length: > MaxComment }) return TeacherReviewErrors.CommentTooLong;
        return new TeacherReview
        {
            Id = TeacherReviewId.New(), ClassId = classId, TeacherId = teacherId, StudentId = studentId, Period = period, Overall = overall,
            Method = method, Clarity = clarity, Enthusiasm = enthusiasm, Punctuality = punctuality, Comment = comment, Anonymous = anonymous, CreatedAt = at,
        };
    }
}
