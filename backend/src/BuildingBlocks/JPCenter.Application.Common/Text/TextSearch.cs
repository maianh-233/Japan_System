using System.Globalization;
using System.Text;

namespace JPCenter.Application.Common.Text;

/// <summary>Chuẩn hóa chữ để tìm kiếm không dấu (dùng chung mọi module): chữ thường, bỏ dấu tiếng Việt (đ → d), gọn khoảng trắng</summary>
public static class TextSearch
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        foreach (var ch in text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch == 'đ' ? 'd' : ch);
        }
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
