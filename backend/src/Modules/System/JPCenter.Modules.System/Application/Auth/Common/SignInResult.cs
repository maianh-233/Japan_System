namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>
/// Kết quả đăng nhập / làm mới phiên. <see cref="Refresh"/> KHÔNG trả trong body — endpoint đặt vào cookie HttpOnly.
/// </summary>
public sealed record SignInResult(SessionUserDto User, string AccessToken, DateTimeOffset AccessTokenExpiresAt, IssuedRefreshToken Refresh);

/// <summary>Refresh token gốc (chỉ tồn tại trong response, DB lưu bản băm).</summary>
/// <param name="Persistent">"Ghi nhớ đăng nhập" → cookie có hạn; không → cookie phiên trình duyệt.</param>
public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt, bool Persistent);
