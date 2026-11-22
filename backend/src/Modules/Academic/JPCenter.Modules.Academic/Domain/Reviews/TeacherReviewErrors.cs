using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>Lỗi đánh giá GV (ND-08)</summary>
public static class TeacherReviewErrors
{
    /// <summary>ND-08.E2</summary>
    public static readonly Error InvalidRating = Error.Validation("academic.teacher_review.invalid_rating", "Đánh giá từ 1 đến 5 sao");
    public static readonly Error CommentTooLong = Error.Validation("academic.teacher_review.comment_too_long", "Góp ý tối đa 2000 ký tự");
    /// <summary>ND-08.E1</summary>
    public static readonly Error Duplicate = Error.Conflict("academic.teacher_review.duplicate", "Bạn đã đánh giá giáo viên này trong đợt này");
    /// <summary>ND-08.E3</summary>
    public static readonly Error NotOpen = Error.Conflict("academic.teacher_review.not_open", "Chưa tới hoặc đã hết thời gian khảo sát");
    public static readonly Error NotYourTeacher = Error.Forbidden("academic.teacher_review.not_your_teacher", "Giáo viên không dạy lớp của bạn");
}
