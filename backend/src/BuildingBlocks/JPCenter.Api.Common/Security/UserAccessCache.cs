using JPCenter.Application.Common.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace JPCenter.Api.Common.Security;

/// <summary>
/// Cache quyền trong bộ nhớ, 5 phút / tài khoản (1 instance, không Redis — ADR 0002).
/// <see cref="InvalidateAll"/> dùng token hủy chung khi ma trận quyền của role đổi.
/// </summary>
public sealed class UserAccessCache(IMemoryCache cache, IServiceScopeFactory scopes) : IUserAccessCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private CancellationTokenSource _all = new();

    public async Task<UserAccess?> GetAsync(Guid accountId, bool isPortalAccount, CancellationToken ct)
    {
        if (cache.TryGetValue(Key(accountId), out UserAccess? hit)) return hit;

        await using var scope = scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetService<IUserAccessProvider>();
        if (provider is null) return null;

        var access = await provider.LoadAsync(accountId, isPortalAccount, ct);
        if (access is null) return null;

        using var entry = cache.CreateEntry(Key(accountId));
        entry.Value = access;
        entry.AbsoluteExpirationRelativeToNow = Ttl;
        entry.AddExpirationToken(new CancellationChangeToken(_all.Token));
        return access;
    }

    public void Invalidate(Guid accountId) => cache.Remove(Key(accountId));

    public void InvalidateAll()
    {
        var old = Interlocked.Exchange(ref _all, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    private static string Key(Guid accountId) => $"access:{accountId}";
}
