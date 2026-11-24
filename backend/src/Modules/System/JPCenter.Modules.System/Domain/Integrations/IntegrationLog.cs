using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Nhật ký gọi / nhận của tích hợp (chỉ thêm; job dọn dòng cũ hơn <see cref="RetentionDays"/> ngày — TH-05). Bảng: system.integration_logs</summary>
public sealed class IntegrationLog : Entity<IntegrationLogId>
{
    public const int RetentionDays = 180;
    public const int MaxMessage = 1000;

    private IntegrationLog() { }

    public IntegrationId IntegrationId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public bool Ok { get; private set; }
    public string Message { get; private set; } = null!;

    public static IntegrationLog Create(IntegrationId integrationId, bool ok, string message, DateTimeOffset at)
    {
        var m = message.Trim();
        return new() { Id = IntegrationLogId.New(), IntegrationId = integrationId, Ok = ok, Message = m.Length > MaxMessage ? m[..MaxMessage] : m, At = at };
    }
}
