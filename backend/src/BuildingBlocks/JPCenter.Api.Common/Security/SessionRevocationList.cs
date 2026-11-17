using JPCenter.Application.Common.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace JPCenter.Api.Common.Security;

/// <summary>Nhớ phiên bị thu hồi trong thời gian sống của access token (+1 phút dự phòng).</summary>
public sealed class SessionRevocationList(IMemoryCache cache, IOptions<JwtOptions> jwt) : ISessionRevocationList
{
    public void Revoke(Guid sessionId) =>
        cache.Set(Key(sessionId), true, TimeSpan.FromMinutes(jwt.Value.AccessTokenMinutes + 1));

    public bool IsRevoked(Guid sessionId) => cache.TryGetValue(Key(sessionId), out _);

    private static string Key(Guid sessionId) => $"revoked-session:{sessionId}";
}
