namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>
/// TV-06 — số liệu đọc của 1 tài liệu, tính từ các lượt xem: lượt, người đọc khác nhau, phút TB (chỉ lượt đã kết thúc), lần cuối,
/// lượt theo 14 ngày gần nhất (cũ → mới, theo ngày ở múi giờ đã cho), top 6 người đọc.
/// </summary>
public sealed record LibraryStats(int Views, int Readers, decimal AvgMinutes, DateTimeOffset? LastViewedAt, IReadOnlyList<int> Daily,
    IReadOnlyList<TopReader> Top)
{
    public const int Days = 14;
    public const int TopCount = 6;

    public static readonly LibraryStats Empty = new(0, 0, 0, null, new int[Days], []);

    /// <param name="views">Các lượt xem của tài liệu</param>
    /// <param name="today">Ngày hôm nay (giờ Việt Nam)</param>
    /// <param name="offset">Độ lệch múi giờ để quy lượt xem về ngày (giờ Việt Nam = +7)</param>
    public static LibraryStats From(IReadOnlyCollection<DocumentView> views, DateOnly today, TimeSpan offset)
    {
        if (views.Count == 0) return Empty;
        var ended = views.Where(v => v.EndedAt is not null).ToList();
        var avg = ended.Count == 0 ? 0 : decimal.Round(ended.Average(v => v.Minutes), 1);
        var daily = new int[Days];
        foreach (var v in views)
        {
            var day = DateOnly.FromDateTime(v.StartedAt.ToOffset(offset).DateTime);
            var back = today.DayNumber - day.DayNumber;
            if (back is >= 0 and < Days) daily[Days - 1 - back]++;
        }
        var top = views.GroupBy(v => (v.ViewerType, v.ViewerId))
            .Select(g => (Last: g.MaxBy(v => v.StartedAt)!, Count: g.Count()))
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.Last.StartedAt)
            .Take(TopCount)
            .Select(x => new TopReader(x.Last.ViewerName, x.Last.ViewerRole ?? RoleOf(x.Last.ViewerType), x.Count))
            .ToList();
        var readers = views.Select(v => (v.ViewerType, v.ViewerId)).Distinct().Count();
        return new LibraryStats(views.Count, readers, avg, views.Max(v => v.StartedAt), daily, top);
    }

    private static string RoleOf(ViewerType type) => type switch
    {
        ViewerType.Student => "Học viên",
        ViewerType.Guardian => "Phụ huynh",
        _ => "Nhân viên",
    };
}
