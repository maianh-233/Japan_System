using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>1 mục Cài đặt chung (key → giá trị JSON). Bảng: system.settings</summary>
public sealed class Setting : Entity<SettingId>, IAuditable
{
    private Setting() { }

    /// <summary>Khóa dạng "nhóm.tên", vd crm.defaultSlaMinutes, invoice.codePattern — duy nhất</summary>
    public string Key { get; private set; } = null!;
    /// <summary>Giá trị dạng JSON (số, chuỗi, bool)</summary>
    public string Value { get; private set; } = null!;

    public static Setting Create(string key, string value) => new() { Id = SettingId.New(), Key = key, Value = value };

    public void Change(string value) => Value = value;
}
