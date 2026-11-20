namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>1 mục cài đặt sau khi phẳng hóa: khóa "nhóm.mục", giá trị nguyên thủy (số / chuỗi / bool / null) và chuỗi hiển thị cho nhật ký.</summary>
/// <param name="Key">Trùng khóa của frontend <c>SETTING_LABELS</c> và cột <c>system.settings.key</c></param>
/// <param name="Value">int · long · bool · string ("HH:mm", "month") · null — tầng Application ghi thành JSON</param>
/// <param name="Display">Hiển thị trong nhật ký trước → sau ("Bật", "500.000đ", "Chủ nhật")</param>
public sealed record SettingEntry(string Key, object? Value, string Display);
