namespace JPCenter.Modules.System.Application.Settings.Common;

/// <summary>CD-01 / CD-02 — bộ cài đặt (đúng hình dạng <c>CenterSettings</c> frontend) + lần sửa gần nhất.</summary>
/// <param name="Changed">Số mục vừa đổi (chỉ có khi lưu)</param>
public sealed record SettingsResponse(
    Dictionary<string, Dictionary<string, object?>> Settings,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    int? Changed = null);
