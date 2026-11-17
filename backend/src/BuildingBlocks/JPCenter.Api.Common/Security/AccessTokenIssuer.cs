using System.Security.Claims;
using System.Text;
using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JPCenter.Api.Common.Security;

/// <summary>Phát access token (JWT HS256) — module System gọi khi đăng nhập / làm mới phiên.</summary>
public sealed class AccessTokenIssuer(IOptions<JwtOptions> options, IClock clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid accountId, Portal portal, int tokenVersion, Guid sessionId)
    {
        var o = options.Value;
        var now = clock.UtcNow;
        var expires = now.AddMinutes(o.AccessTokenMinutes);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaims.Subject, accountId.ToString()),
                new Claim(JwtClaims.Portal, portal.ToString().ToLowerInvariant()),
                new Claim(JwtClaims.TokenVersion, tokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtClaims.SessionId, sessionId.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    internal static SymmetricSecurityKey SigningKey(JwtOptions o) => new(Encoding.UTF8.GetBytes(o.SigningKey));
}
