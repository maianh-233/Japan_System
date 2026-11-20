using JPCenter.Modules.System.Domain.Settings;

namespace JPCenter.Modules.System.Application.Settings.Common;

/// <summary>Bản Cài đặt chung đang dùng, giữ trong bộ nhớ (singleton — hệ thống chạy 1 instance, không cần Redis). Xóa khi lưu.</summary>
public sealed class SettingsCache
{
    private CenterSettings? _value;

    public CenterSettings? Value => Volatile.Read(ref _value);

    public void Set(CenterSettings value) => Volatile.Write(ref _value, value);

    public void Invalidate() => Volatile.Write(ref _value, null);
}
