using System.Collections.Concurrent;
using JPCenter.Application.Common.Idempotency;
using JPCenter.SharedKernel.Domain;

namespace JPCenter.Infrastructure.Common.Idempotency;

/// <summary>
/// Lưu Idempotency-Key trong bộ nhớ, hết hạn sau 24 giờ. Mặc định khi chưa có bản lưu PostgreSQL của module System
/// (khởi động lại app thì mất — chấp nhận được với môi trường dev).
/// </summary>
public sealed class InMemoryIdempotencyStore(IClock clock) : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public Task<IdempotencyBegin> TryBeginAsync(string scope, string key, string requestHash, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var id = $"{scope}:{key}";
        var fresh = new Entry(requestHash, null, now + Ttl);

        while (true)
        {
            if (_entries.TryAdd(id, fresh)) return Task.FromResult(new IdempotencyBegin(IdempotencyState.Started));
            if (!_entries.TryGetValue(id, out var existing)) continue;
            if (existing.ExpiresAt <= now)
            {
                if (_entries.TryUpdate(id, fresh, existing)) return Task.FromResult(new IdempotencyBegin(IdempotencyState.Started));
                continue;
            }
            if (existing.RequestHash != requestHash) return Task.FromResult(new IdempotencyBegin(IdempotencyState.Mismatch));
            return Task.FromResult(existing.Response is null
                ? new IdempotencyBegin(IdempotencyState.InProgress)
                : new IdempotencyBegin(IdempotencyState.Completed, existing.Response));
        }
    }

    public Task CompleteAsync(string scope, string key, StoredResponse response, CancellationToken ct)
    {
        _entries.AddOrUpdate($"{scope}:{key}", _ => new Entry("", response, clock.UtcNow + Ttl), (_, e) => e with { Response = response });
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string scope, string key, CancellationToken ct)
    {
        _entries.TryRemove($"{scope}:{key}", out _);
        return Task.CompletedTask;
    }

    private sealed record Entry(string RequestHash, StoredResponse? Response, DateTimeOffset ExpiresAt);
}
