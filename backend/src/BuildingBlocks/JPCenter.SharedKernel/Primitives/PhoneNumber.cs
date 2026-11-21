using System.Text.RegularExpressions;

namespace JPCenter.SharedKernel.Primitives;

/// <summary>Số điện thoại Việt Nam: chuẩn hóa (bỏ khoảng trắng / dấu chấm / gạch, +84 → 0) và kiểm tra 10 số bắt đầu bằng 0.</summary>
public static partial class PhoneNumber
{
    /// <summary>Chuẩn hóa — chuỗi rỗng → null. Không kiểm tra định dạng (gọi <see cref="IsValid"/>).</summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var p = new string(phone.Trim().Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (p.StartsWith("+84", StringComparison.Ordinal)) return "0" + p[3..];
        return p.StartsWith("84", StringComparison.Ordinal) && p.Length == 11 ? "0" + p[2..] : p;
    }

    /// <summary>Đúng 10 số, bắt đầu bằng 0 (sau khi chuẩn hóa)</summary>
    public static bool IsValid(string? normalized) => normalized is not null && Pattern().IsMatch(normalized);

    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex Pattern();
}
