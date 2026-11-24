using System.Text.RegularExpressions;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// HV-15 bước 2 — mã tra cứu chứng nhận <c>CN-{năm}-{mã lớp rút gọn}-{số:4}</c>, vd lớp GVV_N3-K12 → CN-2026-N3K12-0007
/// (bỏ tiền tố GV / GVV / GVN, chỉ giữ chữ + số, viết hoa — giống frontend). Số thứ tự đếm trong năm (Application lấy max + 1).
/// </summary>
public static partial class CertificateCode
{
    public static string Build(int year, string classCode, int sequence) => $"CN-{year}-{ShortClass(classCode)}-{sequence:0000}";

    public static string ShortClass(string classCode) => NonAlnum().Replace(Prefix().Replace(classCode.Trim(), ""), "").ToUpperInvariant();

    [GeneratedRegex("^GV[VN]*_", RegexOptions.IgnoreCase)]
    private static partial Regex Prefix();

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlnum();
}
