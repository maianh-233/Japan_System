using JPCenter.Application.Common.Auditing;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Auditing;

/// <summary>Nơi lưu mặc định khi chưa có module System — chỉ ghi ra log.</summary>
public sealed class LoggingAuditSink(ILogger<LoggingAuditSink> logger) : IAuditSink
{
    public Task WriteAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct)
    {
        foreach (var e in entries)
            logger.LogInformation("AUDIT {Category}/{Severity} {Actor}: {Action} — {Target}", e.Category, e.Severity, e.ActorName, e.Action, e.Target);
        return Task.CompletedTask;
    }
}
