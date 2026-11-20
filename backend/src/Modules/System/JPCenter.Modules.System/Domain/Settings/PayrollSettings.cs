namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Bảng lương</summary>
/// <param name="LockDay">Ngày trong tháng SAU phải chốt bảng lương tháng trước (quá ngày chưa chốt → cảnh báo) — 1 → 28</param>
public sealed record PayrollSettings(int LockDay);
