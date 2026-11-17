using Microsoft.Extensions.Configuration;

namespace JPCenter.Infrastructure.Common.Email;

/// <summary>
/// Cấu hình gửi email + thông tin thương hiệu in ở chân email. Đọc từ <c>Email:*</c> và <c>App:*</c>
/// (.env: <c>Email__Host</c>, <c>App__Hotline</c>...).
/// </summary>
public sealed record EmailOptions(
    string? Host,
    int Port,
    bool EnableSsl,
    string? Username,
    string? Password,
    string From,
    string FromName,
    /// <summary>Địa chỉ nhận khi người dùng bấm "Trả lời" — hộp thư hỗ trợ (email gửi đi là no-reply)</summary>
    string? ReplyTo,
    string CenterName,
    string Tagline,
    string? Hotline,
    string? SupportEmail,
    string? Address,
    string WebsiteUrl)
{
    public static EmailOptions Load(IConfiguration c) => new(
        c["Email:Host"],
        c.GetValue("Email:Port", 25),
        c.GetValue("Email:EnableSsl", false),
        c["Email:Username"],
        c["Email:Password"],
        c["Email:From"] ?? "no-reply@jpcenter.vn",
        c["Email:FromName"] ?? "JP Center",
        c["Email:ReplyTo"] ?? c["App:SupportEmail"],
        c["App:CenterName"] ?? "JP Center",
        c["App:Tagline"] ?? "Trung tâm Nhật ngữ",
        c["App:Hotline"],
        c["App:SupportEmail"],
        c["App:Address"],
        (c["App:FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/'));
}
