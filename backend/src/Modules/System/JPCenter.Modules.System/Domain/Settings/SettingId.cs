using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Id mục cài đặt</summary>
public readonly record struct SettingId(Guid Value)
{
    public static SettingId New() => new(IdGenerator.NewId());
}
