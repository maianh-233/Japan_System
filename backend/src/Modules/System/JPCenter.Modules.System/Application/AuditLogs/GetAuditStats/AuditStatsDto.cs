namespace JPCenter.Modules.System.Application.AuditLogs.GetAuditStats;

/// <summary>Thẻ đầu trang Nhật ký (NK-01 bước 3)</summary>
/// <param name="Today">Thao tác hôm nay</param>
/// <param name="CriticalWeek">Mức nghiêm trọng trong 7 ngày</param>
/// <param name="ExportWeek">Xuất file trong 7 ngày</param>
/// <param name="FailedLoginWeek">Đăng nhập thất bại trong 7 ngày</param>
public sealed record AuditStatsDto(int Today, int CriticalWeek, int ExportWeek, int FailedLoginWeek);
