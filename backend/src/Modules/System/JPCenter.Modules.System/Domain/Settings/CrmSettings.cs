namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Cài đặt nhóm Tuyển sinh</summary>
/// <param name="DefaultSlaMinutes">SLA liên hệ lead mới (phút) khi không có quy tắc chia lead nào khớp — 1 → 1440</param>
public sealed record CrmSettings(int DefaultSlaMinutes);
