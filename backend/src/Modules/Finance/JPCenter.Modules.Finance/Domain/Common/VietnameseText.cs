using System.Globalization;
using System.Text;

namespace JPCenter.Modules.Finance.Domain.Common;

/// <summary>Chuẩn hóa chữ tiếng Việt cho nội dung chuyển khoản / đối soát (ngân hàng thường bỏ dấu, viết hoa)</summary>
public static class VietnameseText
{
    /// <summary>"Nguyễn Văn Đức" → "NGUYEN VAN DUC" (bỏ dấu, chữ hoa, gộp khoảng trắng)</summary>
    public static string Plain(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c switch { 'đ' => 'd', 'Đ' => 'D', _ => c });
        }
        return string.Join(' ', sb.ToString().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
