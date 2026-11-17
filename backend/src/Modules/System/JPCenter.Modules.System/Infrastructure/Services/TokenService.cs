using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Token / mã bí mật dùng <see cref="RandomNumberGenerator"/> (ngẫu nhiên an toàn mật mã). DB chỉ lưu SHA-256.</summary>
internal sealed class TokenService(AccessTokenIssuer issuer, IOptions<JwtOptions> jwt) : ITokenService
{
    private const string PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(Guid accountId, Portal portal, int tokenVersion, Guid sessionId) =>
        issuer.Issue(accountId, portal, tokenVersion, sessionId);

    public string NewSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public string NewOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public string NewTemporaryPassword()
    {
        while (true)
        {
            var p = RandomNumberGenerator.GetString(PasswordChars, 10);
            if (p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit)) return p;
        }
    }

    public string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public int RememberDays => jwt.Value.RefreshTokenDays;

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
