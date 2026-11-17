using System.Threading.Channels;
using JPCenter.Application.Common.Auditing;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Auditing;

/// <summary>Hàng đợi trong bộ nhớ — request chỉ đẩy vào rồi đi tiếp; <see cref="AuditBackgroundWriter"/> ghi theo lô.</summary>
public sealed class ChannelAuditWriter(ILogger<ChannelAuditWriter> logger) : IAuditWriter
{
    private readonly Channel<AuditEntry> _channel = Channel.CreateBounded<AuditEntry>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    internal ChannelReader<AuditEntry> Reader => _channel.Reader;

    public void Enqueue(AuditEntry entry)
    {
        // Hàng đợi đầy (DB chết lâu) → không chặn request, nhưng phải để lại dấu vết trong log
        if (!_channel.Writer.TryWrite(entry))
            logger.LogError("Hàng đợi nhật ký đầy — mất dòng audit {Action} {Target} của {Actor}", entry.Action, entry.Target, entry.ActorName);
    }
}
