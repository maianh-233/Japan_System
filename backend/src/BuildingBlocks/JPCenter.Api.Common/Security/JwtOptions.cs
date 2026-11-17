namespace JPCenter.Api.Common.Security;

/// <summary>Cấu hình JWT (mục <c>Jwt</c> — biến môi trường <c>Jwt__*</c> trong .env).</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "jpcenter-api";
    public string Audience { get; set; } = "jpcenter-web";

    /// <summary>Khóa ký HS256 — tối thiểu 32 ký tự.</summary>
    public string SigningKey { get; set; } = "";

    /// <summary>Access token sống ngắn (AUTH-02: 15 phút).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token khi có "Ghi nhớ đăng nhập" (AUTH-02: 30 ngày).</summary>
    public int RefreshTokenDays { get; set; } = 30;
}
