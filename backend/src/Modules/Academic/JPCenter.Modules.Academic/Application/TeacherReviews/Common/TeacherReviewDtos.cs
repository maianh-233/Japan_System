using JPCenter.Modules.Academic.Domain.Reviews;

namespace JPCenter.Modules.Academic.Application.TeacherReviews.Common;

/// <summary>4 tiêu chí 1–5 sao — khớp <c>ratings: Record&lt;ReviewCriterion, number&gt;</c></summary>
public sealed record ReviewRatingsDto(int Method, int Clarity, int Enthusiasm, int Punctuality);

/// <summary>
/// Đánh giá GV — khớp <c>TeacherReview</c> (frontend). Ở cổng GV: đánh giá ẩn danh không có <c>StudentId</c> / <c>StudentName</c> (ND-08.E4).
/// </summary>
public sealed record TeacherReviewDto(
    Guid Id,
    Guid ClassId,
    string ClassCode,
    Guid TeacherId,
    string TeacherName,
    Guid? StudentId,
    string? StudentName,
    ReviewPeriod Period,
    int Overall,
    ReviewRatingsDto Ratings,
    string? Comment,
    bool Anonymous,
    DateTimeOffset CreatedAt);

/// <summary>GV của lớp trong đợt khảo sát + HV đã đánh giá chưa</summary>
public sealed record SurveyTeacherDto(Guid TeacherId, string Name, bool Reviewed);

/// <summary>Khảo sát đánh giá GV của 1 lớp ở cổng HV: <c>Period</c> null = ngoài thời gian khảo sát</summary>
public sealed record ReviewSurveyDto(Guid ClassId, string ClassCode, string ClassName, ReviewPeriod? Period, IReadOnlyList<SurveyTeacherDto> Teachers);
