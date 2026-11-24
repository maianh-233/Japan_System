namespace JPCenter.Modules.Reporting.Domain.TodayWork;

/// <summary>Thời lượng dạng người đọc (BC-02): &lt; 60 phút → "N phút", &lt; 24 giờ → "N giờ", còn lại "N ngày" — làm tròn xuống</summary>
public static class HumanDuration
{
    public static string Of(int minutes)
    {
        var m = Math.Max(0, minutes);
        if (m < 60) return $"{m} phút";
        if (m < 60 * 24) return $"{m / 60} giờ";
        return $"{m / 60 / 24} ngày";
    }
}
