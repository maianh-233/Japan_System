using System.Text.RegularExpressions;
using JPCenter.Modules.Finance.Domain.Common;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Mã hóa đơn theo mẫu ở Cài đặt chung (TC-04 bước 2): <c>{CN}</c> tiền tố chi nhánh · <c>{YYYY}</c> <c>{YY}</c> <c>{MM}</c> · <c>{SO}</c> / <c>{SO:n}</c>.
/// Số thứ tự tăng theo chi nhánh + kỳ đánh số lại (<see cref="PeriodKey"/>) — bộ đếm có khóa ở Infrastructure (E4).
/// Nội dung chuyển khoản = mã + tên HV không dấu (E3 — đối soát tự động).
/// </summary>
public static partial class InvoiceCode
{
    /// <summary>Độ dài tối đa nội dung chuyển khoản (giới hạn chung của các ngân hàng cho VietQR)</summary>
    public const int MaxTransferContent = 50;

    public static string Format(string pattern, string branchPrefix, DateOnly date, int sequence) =>
        Sequence().Replace(
            pattern.Replace("{CN}", branchPrefix.ToUpperInvariant()).Replace("{YYYY}", date.ToString("yyyy"))
                .Replace("{YY}", date.ToString("yy")).Replace("{MM}", date.ToString("MM")),
            m => sequence.ToString().PadLeft(m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 1, '0'));

    /// <summary>Kỳ đánh số: "month" → 2026-11 · "year" → 2026 · "never" → all</summary>
    public static string PeriodKey(string resetEvery, DateOnly date) => resetEvery switch
    {
        "month" => date.ToString("yyyy-MM"),
        "year" => date.ToString("yyyy"),
        _ => "all",
    };

    /// <summary>"HD-CG-2611-0003" + "Nguyễn Văn An" → "HD-CG-2611-0003 NGUYEN VAN AN" (cắt ≤ 50 ký tự, mã luôn giữ nguyên)</summary>
    public static string TransferContent(string code, string studentName)
    {
        var name = NotAllowed().Replace(VietnameseText.Plain(studentName), string.Empty).Trim();
        var content = string.IsNullOrEmpty(name) ? code : $"{code} {name}";
        return content.Length <= MaxTransferContent ? content : content[..MaxTransferContent].TrimEnd();
    }

    /// <summary>Nội dung chuyển khoản có chứa mã hóa đơn này (không phân biệt hoa thường, dấu, khoảng trắng / gạch thay nhau)</summary>
    public static bool Mentions(string content, string code)
    {
        static string Compact(string s) => Separators().Replace(VietnameseText.Plain(s), string.Empty);
        return Compact(content).Contains(Compact(code), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\{SO(?::([1-9]))?\}")]
    private static partial Regex Sequence();

    [GeneratedRegex(@"[^A-Z0-9 ]")]
    private static partial Regex NotAllowed();

    [GeneratedRegex(@"[\s\-_.]")]
    private static partial Regex Separators();
}
