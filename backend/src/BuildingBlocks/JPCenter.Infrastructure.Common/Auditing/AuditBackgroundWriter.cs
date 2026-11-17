using JPCenter.Application.Common.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Auditing;

/// <summary>Đọc hàng đợi nhật ký, ghi theo lô (tối đa 200 dòng / lần) qua <see cref="IAuditSink"/>.</summary>
public sealed class AuditBackgroundWriter(ChannelAuditWriter writer, IServiceScopeFactory scopes, ILogger<AuditBackgroundWriter> logger)
    : BackgroundService
{
    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = writer.Reader;
        var batch = new List<AuditEntry>(BatchSize);
        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < BatchSize && reader.TryRead(out var entry)) batch.Add(entry);
                await FlushAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Tắt ứng dụng: ghi nốt phần còn lại
            while (reader.TryRead(out var entry)) batch.Add(entry);
            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<AuditEntry> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAuditSink>().WriteAsync(batch.ToList(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Không ghi được {Count} dòng nhật ký", batch.Count);
        }
        batch.Clear();
    }
}
