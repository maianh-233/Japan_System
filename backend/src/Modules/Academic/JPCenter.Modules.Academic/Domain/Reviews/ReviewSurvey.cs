namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>
/// Thời gian mở khảo sát đánh giá GV (ND-08): giữa kỳ — từ ngày thi giữa kỳ của lớp tới trước đợt cuối kỳ; cuối kỳ — 14 ngày trước tới 7 ngày sau
/// ngày kết thúc lớp. Lớp không có mốc thi giữa kỳ → chỉ có đợt cuối kỳ.
/// </summary>
public static class ReviewSurvey
{
    public const int FinalOpensDaysBefore = 14;
    public const int FinalClosesDaysAfter = 7;

    /// <param name="midExamDate">Ngày buổi thi giữa kỳ (null = lớp không có / chưa xếp)</param>
    /// <param name="classEnd">Ngày kết thúc (dự kiến / thực tế) của lớp</param>
    public static ReviewPeriod? OpenPeriod(DateOnly today, DateOnly? midExamDate, DateOnly classEnd)
    {
        var finalFrom = classEnd.AddDays(-FinalOpensDaysBefore);
        if (today >= finalFrom && today <= classEnd.AddDays(FinalClosesDaysAfter)) return ReviewPeriod.Final;
        if (midExamDate is { } mid && today >= mid && today < finalFrom) return ReviewPeriod.Mid;
        return null;
    }
}
