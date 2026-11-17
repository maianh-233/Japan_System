using System.Collections.Concurrent;
using System.Text.Json;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Events;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>
/// Hiện thực <see cref="IIntegrationEventPublisher"/> (scoped / request): gom sự kiện, <see cref="OutboxInterceptor"/> đổ vào
/// <c>outbox_messages</c> của DbContext được lưu KẾ TIẾP trong request — nên publish ngay trước SaveChanges của module mình.
/// </summary>
public sealed class OutboxCollector(IClock clock) : IIntegrationEventPublisher
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentQueue<OutboxMessage> _pending = new();

    public void Publish<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
    {
        var now = clock.UtcNow;
        _pending.Enqueue(new OutboxMessage
        {
            Id = IdGenerator.NewId(),
            Type = typeof(TEvent).FullName!,
            Payload = JsonSerializer.Serialize(@event, Json),
            OccurredAt = now,
            NextAttemptAt = now,
        });
    }

    public List<OutboxMessage> Drain()
    {
        var list = new List<OutboxMessage>();
        while (_pending.TryDequeue(out var m)) list.Add(m);
        return list;
    }
}
