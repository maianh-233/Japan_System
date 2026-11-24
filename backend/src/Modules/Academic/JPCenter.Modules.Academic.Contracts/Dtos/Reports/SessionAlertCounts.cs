namespace JPCenter.Modules.Academic.Contracts.Dtos.Reports;

/// <summary>Đếm buổi cần nhắc ở bảng điều khiển (BC-01 bước 4).</summary>
/// <param name="Unconfirmed">Buổi đã dạy chưa được xác nhận</param>
/// <param name="MissingLogs">Buổi đã dạy GV chưa ghi báo cáo buổi</param>
public sealed record SessionAlertCounts(int Unconfirmed, int MissingLogs);
