using System.Text.RegularExpressions;

namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Mã hóa đơn</summary>
/// <param name="CodePattern">Mẫu: {CN} tiền tố chi nhánh · {YYYY} {YY} {MM} năm / tháng · {SO} hoặc {SO:n} số thứ tự n chữ số (bắt buộc)</param>
/// <param name="ResetEvery">Đánh số lại từ 1 mỗi kỳ</param>
public sealed partial record InvoiceSettings(string CodePattern, InvoiceNumberReset ResetEvery)
{
    public const int MaxPatternLength = 40;

    /// <summary>Mẫu hợp lệ: ≤ 40 ký tự, chỉ dùng biến đã biết, có đúng 1 biến số thứ tự, không còn dấu ngoặc lẻ.</summary>
    public bool IsPatternValid()
    {
        if (string.IsNullOrWhiteSpace(CodePattern) || CodePattern.Length > MaxPatternLength) return false;
        var tokens = Token().Matches(CodePattern).Select(m => m.Value).ToList();
        if (tokens.Any(t => !Allowed().IsMatch(t))) return false;
        if (tokens.Count(t => t.StartsWith("{SO", StringComparison.Ordinal)) != 1) return false;
        var rest = Token().Replace(CodePattern, string.Empty);
        return !rest.Contains('{') && !rest.Contains('}');
    }

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Token();

    [GeneratedRegex(@"^\{(CN|YYYY|YY|MM|SO|SO:[1-9])\}$")]
    private static partial Regex Allowed();
}
