using System.Collections.Concurrent;

namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>Đánh thức tiến trình gửi của đúng module khi vừa có sự kiện mới (singleton).</summary>
public sealed class OutboxSignal
{
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _signals = new();

    private SemaphoreSlim Of(Type contextType) => _signals.GetOrAdd(contextType, _ => new SemaphoreSlim(0, 1));

    public void Notify(Type contextType)
    {
        var s = Of(contextType);
        // Đã có tín hiệu chờ thì thôi (tối đa 1) — tiến trình gửi lấy cả lô
        if (s.CurrentCount == 0) try { s.Release(); } catch (SemaphoreFullException) { }
    }

    /// <summary>Chờ tín hiệu hoặc hết <paramref name="timeout"/>.</summary>
    public Task WaitAsync(Type contextType, TimeSpan timeout, CancellationToken ct) => Of(contextType).WaitAsync(timeout, ct);
}
