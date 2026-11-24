namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>
/// Quy tắc vàng 2 (user story 16) — dòng watermark in lặp trên trình xem: "&lt;tên&gt; · &lt;email / mã&gt; · dd/MM/yyyy HH:mm".
/// Thời điểm truyền vào đã đổi sang giờ Việt Nam (ở rìa API).
/// </summary>
public static class Watermark
{
    public static string Text(string viewerName, string? contact, DateTime localTime)
    {
        var parts = new[] { viewerName.Trim(), contact?.Trim(), localTime.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) };
        return string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
