namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Giờ hành chính (chấm công nhân viên)</summary>
/// <param name="From">Bắt đầu T2–T6</param>
/// <param name="To">Kết thúc T2–T6 — sau <paramref name="From"/></param>
/// <param name="SatTo">Thứ 7 làm đến; null = nghỉ thứ 7</param>
/// <param name="LateGraceMin">Đi muộn / về sớm quá N phút mới tính vi phạm — 0 → 120</param>
public sealed record OfficeSettings(TimeOnly From, TimeOnly To, TimeOnly? SatTo, int LateGraceMin);
