using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Kết nối dịch vụ ngoài (Zalo OA, VietQR, Zoom, email, Facebook lead...). Bảng: system.integrations</summary>
public sealed class Integration : AggregateRoot<IntegrationId>, IAuditable
{
    private Integration() { }

    /// <summary>Khóa cố định, vd zalo_oa, vietqr, zoom — duy nhất</summary>
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public IntegrationCategory Category { get; private set; }
    public IntegrationStatus Status { get; private set; }
    /// <summary>Tài khoản / trang đã kết nối</summary>
    public string? Account { get; private set; }
    public DateTimeOffset? ConnectedAt { get; private set; }
    public Guid? ConnectedBy { get; private set; }
    public DateTimeOffset? LastSyncAt { get; private set; }
    public string? LastError { get; private set; }
    /// <summary>Cấu hình key → value; trường bí mật được mã hóa trước khi lưu (jsonb)</summary>
    public Dictionary<string, string> Config { get; private set; } = [];
    /// <summary>Sự kiện dùng kênh này: key → bật / tắt (jsonb)</summary>
    public Dictionary<string, bool> Events { get; private set; } = [];
    /// <summary>URL webhook hệ thống cấp cho bên ngoài gọi vào</summary>
    public string? WebhookUrl { get; private set; }

    public static Integration Create(string key, string name, string description, IntegrationCategory category) =>
        new() { Id = IntegrationId.New(), Key = key, Name = name, Description = description, Category = category, Status = IntegrationStatus.Disconnected };

    public void Connect(Dictionary<string, string> config, string? account, Guid by, DateTimeOffset at) =>
        (Config, Account, ConnectedBy, ConnectedAt, Status, LastError) = (config, account, by, at, IntegrationStatus.Connected, null);

    public void Disconnect() => (Status, Account) = (IntegrationStatus.Disconnected, null);
    public void MarkError(string error) => (Status, LastError) = (IntegrationStatus.Error, error);
    public void MarkSynced(DateTimeOffset at) => LastSyncAt = at;
    public void ToggleEvent(string eventKey, bool on) => Events[eventKey] = on;
    public void SetWebhook(string? url) => WebhookUrl = url;
}
