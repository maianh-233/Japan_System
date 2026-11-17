using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>
/// Trước khi lưu: đổ sự kiện đang gom vào <c>outbox_messages</c> của chính DbContext đó (cùng giao dịch).
/// Lưu xong: đánh thức tiến trình gửi của module để sự kiện đi ngay (không chờ chu kỳ kiểm tra).
/// </summary>
public sealed class OutboxInterceptor(OutboxCollector collector, OutboxSignal signal) : SaveChangesInterceptor
{
    private bool _added;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Wake(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Wake(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        var messages = collector.Drain();
        if (messages.Count == 0) return;
        context.Set<OutboxMessage>().AddRange(messages);
        _added = true;
    }

    private void Wake(DbContext? context)
    {
        if (!_added || context is null) return;
        _added = false;
        signal.Notify(context.GetType());
    }
}
