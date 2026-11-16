using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Nhật ký gọi / nhận của tích hợp (chỉ thêm). Bảng: system.integration_logs</summary>
public sealed class IntegrationLog : Entity<IntegrationLogId>
{
    private IntegrationLog() { }

    public IntegrationId IntegrationId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public bool Ok { get; private set; }
    public string Message { get; private set; } = null!;

    public static IntegrationLog Create(IntegrationId integrationId, bool ok, string message, DateTimeOffset at) =>
        new() { Id = IntegrationLogId.New(), IntegrationId = integrationId, Ok = ok, Message = message, At = at };
}
