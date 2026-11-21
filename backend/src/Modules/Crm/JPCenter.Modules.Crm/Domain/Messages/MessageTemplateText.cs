using System.Text.RegularExpressions;

namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Điền biến <c>{ten}</c>, <c>{lop}</c>, <c>{so_tien}</c>… vào nội dung — giống <c>messages.ts › fillTemplate / missingVars</c>.</summary>
public static partial class MessageTemplateText
{
    /// <summary>Điền biến; biến chưa có giá trị giữ nguyên để người gửi thấy và sửa tay (CRM-17.E2)</summary>
    public static string Fill(string body, IReadOnlyDictionary<string, string?> vars) =>
        Variable().Replace(body, m => vars.TryGetValue(m.Groups[1].Value, out var v) && !string.IsNullOrEmpty(v) ? v : m.Value);

    /// <summary>Biến còn sót trong nội dung (chưa có dữ liệu) — cảnh báo trước khi gửi</summary>
    public static IReadOnlyList<string> MissingVars(string text) =>
        Variable().Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex Variable();
}
