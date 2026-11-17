using System.Reflection;
using System.Text.Json;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>
/// Gửi sự kiện trong <c>outbox_messages</c> của 1 module tới mọi <see cref="IIntegrationEventHandler{TEvent}"/> (mọi module).
/// Thức dậy ngay khi module vừa lưu sự kiện, hoặc theo chu kỳ dự phòng. Mỗi handler chạy trong scope riêng;
/// lỗi → thử lại lùi dần, chỉ chạy lại handler còn lỗi; quá <see cref="OutboxOptions.MaxAttempts"/> → đánh dấu thất bại + log lỗi.
/// Đăng ký tự động bởi <c>AddModuleDbContext</c>.
/// </summary>
public sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopes, OutboxSignal signal, OutboxOptions options, IClock clock, ILogger<OutboxProcessor<TContext>> logger)
    : BackgroundService where TContext : ModuleDbContextBase
{
    private static readonly MethodInfo DispatchMethod = typeof(OutboxProcessor<TContext>)
        .GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Xử lý hết lô này tới lô khác cho tới khi không còn gì đến hạn
                while (await ProcessBatchAsync(stoppingToken) > 0 && !stoppingToken.IsCancellationRequested) { }
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox {Context}: lỗi khi đọc / ghi bảng outbox", typeof(TContext).Name);
            }

            try
            {
                await signal.WaitAsync(typeof(TContext), options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Gửi 1 lô; trả số sự kiện đã xử lý (thành công hoặc thất bại) — 0 = không còn gì đến hạn.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = clock.UtcNow;
        var batch = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.FailedAt == null && m.NextAttemptAt <= now)
            .OrderBy(m => m.OccurredAt)
            .Take(options.BatchSize)
            .ToListAsync(ct);

        foreach (var message in batch) await DeliverAsync(message, ct);
        if (batch.Count > 0) await db.SaveChangesAsync(ct);
        return batch.Count;
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        var type = IntegrationEventTypes.Find(message.Type);
        if (type is null)
        {
            Fail(message, $"Không tìm thấy kiểu sự kiện {message.Type}", permanent: true);
            return;
        }

        object @event;
        try
        {
            @event = JsonSerializer.Deserialize(message.Payload, type, OutboxCollector.Json)!;
        }
        catch (JsonException ex)
        {
            Fail(message, $"Không đọc được nội dung sự kiện: {ex.Message}", permanent: true);
            return;
        }

        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(type);
        string? error = null;

        // Lấy danh sách handler 1 lần để biết tên; mỗi handler chạy trong scope riêng (DbContext riêng, lỗi không lan sang nhau)
        List<string> names;
        await using (var probe = scopes.CreateAsyncScope())
            names = probe.ServiceProvider.GetServices(handlerType).Select(h => h!.GetType().FullName!).Distinct().ToList();

        foreach (var name in names.Where(n => !message.HandledBy.Contains(n)))
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices(handlerType).First(h => h!.GetType().FullName == name)!;
            try
            {
                await (Task)DispatchMethod.MakeGenericMethod(type).Invoke(null, [handler, @event, ct])!;
                message.HandledBy.Add(name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                var inner = ex is TargetInvocationException { InnerException: { } i } ? i : ex;
                error = $"{name}: {inner.Message}";
                logger.LogWarning(inner, "Outbox: handler {Handler} lỗi khi xử lý {Event} (lần {Attempt})", name, type.Name, message.Attempts + 1);
            }
        }

        if (error is null)
        {
            message.ProcessedAt = clock.UtcNow;
            message.LastError = null;
        }
        else
        {
            Fail(message, error, permanent: false);
        }
    }

    private void Fail(OutboxMessage message, string error, bool permanent)
    {
        message.Attempts++;
        message.LastError = error.Length > 2000 ? error[..2000] : error;
        if (permanent || message.Attempts >= options.MaxAttempts)
        {
            message.FailedAt = clock.UtcNow;
            logger.LogError("Outbox {Context}: bỏ sự kiện {Id} ({Type}) sau {Attempts} lần — {Error}",
                typeof(TContext).Name, message.Id, message.Type, message.Attempts, message.LastError);
        }
        else
        {
            message.NextAttemptAt = clock.UtcNow + options.Backoff(message.Attempts);
        }
    }

    private static Task DispatchAsync<TEvent>(object handler, object @event, CancellationToken ct) where TEvent : IIntegrationEvent =>
        ((IIntegrationEventHandler<TEvent>)handler).Handle((TEvent)@event, ct);

    /// <summary>Mỗi giờ xóa sự kiện đã gửi xong quá hạn giữ (sự kiện thất bại giữ lại để tra lỗi)</summary>
    private async Task CleanupAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (now - _lastCleanup < TimeSpan.FromHours(1)) return;
        _lastCleanup = now;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var before = now - options.KeepProcessed;
        await db.Set<OutboxMessage>().Where(m => m.ProcessedAt != null && m.ProcessedAt < before).ExecuteDeleteAsync(ct);
    }
}
