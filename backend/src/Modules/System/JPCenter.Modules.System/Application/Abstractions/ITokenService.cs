using JPCenter.Application.Common.Security;

namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>Phát access token, sinh chuỗi bí mật (refresh token, link mời, OTP) và băm chúng — chỉ lưu bản băm trong DB.</summary>
public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(Guid accountId, Portal portal, int tokenVersion, Guid sessionId);

    /// <summary>Chuỗi ngẫu nhiên 256 bit (base64url) — refresh token, link mời, vé đặt mật khẩu.</summary>
    string NewSecret();

    /// <summary>Mã OTP 6 chữ số.</summary>
    string NewOtp();

    /// <summary>Mật khẩu tạm 10 ký tự dễ đọc (không có 0/O, 1/l/I), luôn có chữ hoa, chữ thường, số.</summary>
    string NewTemporaryPassword();

    /// <summary>SHA-256 (hex) — lưu DB thay cho giá trị gốc.</summary>
    string Hash(string secret);

    /// <summary>Số ngày sống của refresh token khi "Ghi nhớ đăng nhập" (AUTH-02: 30).</summary>
    int RememberDays { get; }
}
