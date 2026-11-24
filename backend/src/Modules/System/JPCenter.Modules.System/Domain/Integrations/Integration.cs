using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>
/// Kết nối dịch vụ ngoài (Zalo OA, SMS, email, VietQR, Zoom / Meet, nguồn lead) — story 15. Trường cấu hình / việc theo <see cref="IntegrationCatalog"/>.
/// Giá trị bí mật được tầng hạ tầng mã hóa trước khi gọi <see cref="Connect"/>; domain chỉ giữ / chép nguyên. Bảng: system.integrations
/// </summary>
public sealed class Integration : AggregateRoot<IntegrationId>, IAuditable
{
    private Integration() { }

    /// <summary>Khóa cố định trong <see cref="IntegrationKeys"/> — duy nhất</summary>
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
    /// <summary>Cấu hình key → value; trường bí mật đã mã hóa (jsonb)</summary>
    public Dictionary<string, string> Config { get; private set; } = [];
    /// <summary>Sự kiện dùng kênh này: key → bật / tắt (jsonb)</summary>
    public Dictionary<string, bool> Events { get; private set; } = [];
    /// <summary>URL webhook hệ thống cấp cho bên ngoài gọi vào</summary>
    public string? WebhookUrl { get; private set; }

    public IntegrationSpec Spec => IntegrationCatalog.Find(Key) ?? throw new InvalidOperationException($"Tích hợp '{Key}' không có trong danh mục");

    public static Result<Integration> Create(string key, string name, string description)
    {
        if (IntegrationCatalog.Find(key) is not { } spec) return IntegrationErrors.Unknown;
        return new Integration
        {
            Id = IntegrationId.New(), Key = key, Name = name, Description = description, Category = spec.Category, Status = IntegrationStatus.Disconnected,
            Events = spec.Events.ToDictionary(e => e, _ => false),
        };
    }

    /// <summary>
    /// TH-02 — kết nối / kết nối lại. Trường bắt buộc trống → lỗi (E1), trừ trường bí mật đã có giá trị cũ (giữ khóa cũ — E2).
    /// Trường ngoài danh mục bị bỏ (E4). Xóa lỗi cũ, trạng thái <c>connected</c>.
    /// </summary>
    public Result Connect(IReadOnlyDictionary<string, string?> input, string? account, Guid by, DateTimeOffset at)
    {
        var config = new Dictionary<string, string>();
        foreach (var f in Spec.Fields)
        {
            var value = input.GetValueOrDefault(f.Key)?.Trim();
            if (string.IsNullOrEmpty(value) && f.Secret && Config.TryGetValue(f.Key, out var old)) value = old;
            if (string.IsNullOrEmpty(value))
            {
                if (f.Required) return IntegrationErrors.MissingField(f);
                continue;
            }
            config[f.Key] = value;
        }
        (Config, Account, ConnectedBy, ConnectedAt, Status, LastError) =
            (config, string.IsNullOrWhiteSpace(account) ? null : account.Trim(), by, at, IntegrationStatus.Connected, null);
        return Result.Success();
    }

    /// <summary>TH-02 — ngắt: xóa cấu hình, tắt hết việc</summary>
    public void Disconnect()
    {
        (Status, Account, LastError, Config) = (IntegrationStatus.Disconnected, null, null, []);
        foreach (var e in Events.Keys.ToList()) Events[e] = false;
    }

    /// <summary>TH-03 — bật / tắt 1 việc. Chỉ bật được khi đã kết nối (kể cả đang lỗi — để sửa lỗi xong chạy lại ngay)</summary>
    public Result SetEvent(string eventKey, bool on)
    {
        if (!Spec.Events.Contains(eventKey)) return IntegrationErrors.UnknownEvent;
        if (on && Status == IntegrationStatus.Disconnected) return IntegrationErrors.NotConnected;
        Events[eventKey] = on;
        return Result.Success();
    }

    /// <summary>Kênh dùng được cho việc <paramref name="eventKey"/>: đã kết nối (không lỗi) + việc đang bật</summary>
    public bool IsActive(string eventKey) => Status == IntegrationStatus.Connected && Events.GetValueOrDefault(eventKey);

    /// <summary>
    /// TH-04, TH-06 — ghi 1 lần gọi / nhận. Thành công → cập nhật lần đồng bộ, hết lỗi. Lỗi xác thực (<paramref name="authFailure"/>) →
    /// <c>error</c> + <see cref="LastError"/>; lỗi tạm thời chỉ ghi nhật ký.
    /// </summary>
    public IntegrationLog RecordCall(bool ok, string message, DateTimeOffset at, bool authFailure = false)
    {
        if (ok)
        {
            LastSyncAt = at;
            if (Status == IntegrationStatus.Error) (Status, LastError) = (IntegrationStatus.Connected, null);
        }
        else if (authFailure && Status != IntegrationStatus.Disconnected) MarkError(message);
        return IntegrationLog.Create(Id, ok, message, at);
    }

    /// <summary>Cấu hình trả ra API: trường bí mật chỉ còn 4 ký tự cuối (TH-01.E2). <paramref name="reveal"/> giải mã giá trị đã lưu.</summary>
    public Dictionary<string, string> MaskedConfig(Func<string, string> reveal) =>
        Config.ToDictionary(kv => kv.Key, kv => Spec.Field(kv.Key)?.Secret == true ? Mask(reveal(kv.Value)) : kv.Value);

    /// <summary>Đổi giá trị các trường bí mật (vd mã hóa dữ liệu cũ còn để chữ thường). Trả true nếu có thay đổi.</summary>
    public bool RewriteSecrets(Func<string, string> rewrite)
    {
        var next = Config.ToDictionary(kv => kv.Key, kv => Spec.Field(kv.Key)?.Secret == true ? rewrite(kv.Value) : kv.Value);
        if (next.All(kv => Config[kv.Key] == kv.Value)) return false;
        Config = next;
        return true;
    }

    public static string Mask(string value) => value.Length <= 4 ? "••••••" : "••••••" + value[^4..];

    public void MarkError(string error) => (Status, LastError) = (IntegrationStatus.Error, error.Length > 500 ? error[..500] : error);
    public void MarkSynced(DateTimeOffset at) => LastSyncAt = at;
    public void SetWebhook(string? url) => WebhookUrl = url;
}
