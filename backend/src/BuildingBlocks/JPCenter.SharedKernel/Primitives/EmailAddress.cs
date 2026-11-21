using System.Text.RegularExpressions;

namespace JPCenter.SharedKernel.Primitives;

/// <summary>Email liên hệ: chuẩn hóa (bỏ khoảng trắng, chữ thường) và kiểm tra dạng a@b.c.</summary>
public static partial class EmailAddress
{
    /// <summary>Chuẩn hóa — chuỗi rỗng → null. Không kiểm tra định dạng (gọi <see cref="IsValid"/>).</summary>
    public static string? Normalize(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public static bool IsValid(string? normalized) => normalized is not null && Pattern().IsMatch(normalized);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();
}
