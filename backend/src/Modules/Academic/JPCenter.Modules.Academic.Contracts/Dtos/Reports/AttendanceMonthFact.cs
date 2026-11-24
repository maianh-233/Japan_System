namespace JPCenter.Modules.Academic.Contracts.Dtos.Reports;

/// <summary>Lượt điểm danh của 1 chi nhánh trong 1 tháng (BC-05 bước 3).</summary>
/// <param name="Month">Ngày 1 của tháng</param>
/// <param name="Attended">Có mặt + đi muộn</param>
/// <param name="Total">Mọi lượt đã điểm danh</param>
public sealed record AttendanceMonthFact(Guid BranchId, DateOnly Month, int Attended, int Total);
