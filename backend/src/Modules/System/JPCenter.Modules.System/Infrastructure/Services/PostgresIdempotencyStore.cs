using JPCenter.Application.Common.Idempotency;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Idempotency-Key lưu trong <c>system.idempotency_keys</c> (hạn 24 giờ) — khởi động lại app vẫn còn.
/// Giành key bằng <c>INSERT … ON CONFLICT DO NOTHING</c> → 2 request trùng đến cùng lúc chỉ 1 cái được chạy.
/// </summary>
internal sealed class PostgresIdempotencyStore(SystemDbContext db, IClock clock) : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<IdempotencyBegin> TryBeginAsync(string scope, string key, string requestHash, CancellationToken ct)
    {
        var now = clock.UtcNow;
        // Key hết hạn coi như chưa từng có
        await db.IdempotencyKeys.Where(x => x.Scope == scope && x.Key == key && x.ExpiresAt <= now).ExecuteDeleteAsync(ct);

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO system.idempotency_keys (scope, key, request_hash, created_at, expires_at)
            VALUES ({scope}, {key}, {requestHash}, {now}, {now + Ttl})
            ON CONFLICT (scope, key) DO NOTHING
            """, ct);
        if (inserted == 1) return new IdempotencyBegin(IdempotencyState.Started);

        var existing = await db.IdempotencyKeys.AsNoTracking().FirstAsync(x => x.Scope == scope && x.Key == key, ct);
        if (existing.RequestHash != requestHash) return new IdempotencyBegin(IdempotencyState.Mismatch);
        return existing.StatusCode is { } status
            ? new IdempotencyBegin(IdempotencyState.Completed, new StoredResponse(status, existing.Body, existing.ContentType))
            : new IdempotencyBegin(IdempotencyState.InProgress);
    }

    public Task CompleteAsync(string scope, string key, StoredResponse response, CancellationToken ct) =>
        db.IdempotencyKeys.Where(x => x.Scope == scope && x.Key == key).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.StatusCode, response.StatusCode)
            .SetProperty(x => x.Body, response.Body)
            .SetProperty(x => x.ContentType, response.ContentType), ct);

    public Task ReleaseAsync(string scope, string key, CancellationToken ct) =>
        db.IdempotencyKeys.Where(x => x.Scope == scope && x.Key == key).ExecuteDeleteAsync(ct);
}
