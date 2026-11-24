namespace JPCenter.Modules.Reporting.Application.Reports.Common;

/// <summary>Báo cáo BC-03 → 06 — cùng hình dạng <c>ReportData</c> của <c>features/reports/useReports.ts</c> (frontend tự gắn nhãn nguồn / trạng thái).</summary>
/// <param name="BranchId">Chi nhánh đang xem; null = toàn hệ thống / nhiều chi nhánh trong phạm vi</param>
/// <param name="MonthKeys">Các tháng trong kỳ <c>YYYY-MM</c>, cũ → mới</param>
public sealed record ReportDto(int Months, Guid? BranchId, IReadOnlyList<string> MonthKeys, LeadReportDto Lead, MoneyReportDto Money,
    StudyReportDto Study, IReadOnlyList<BranchRowDto> Branches);

/// <param name="Value">null = không có số (vd chuyên cần tháng chưa điểm danh — BC-05.E1)</param>
public sealed record MonthPointDto(string Month, string Label, decimal? Value);

/// <summary>BC-03</summary>
/// <param name="Open">Đang theo đuổi: mới + tư vấn + hẹn test</param>
public sealed record LeadReportDto(int Total, int Won, int? Rate, int Open, IReadOnlyList<KeyCountDto> Funnel, IReadOnlyList<SourceRowDto> BySource,
    IReadOnlyList<SaleRowDto> BySale, IReadOnlyList<CampaignRowDto> ByCampaign, IReadOnlyList<MonthPointDto> ByMonth);

/// <param name="Key">Trạng thái lead: new · consulting · test_scheduled · won · lost</param>
public sealed record KeyCountDto(string Key, int Value);

/// <param name="Key">Nguồn lead (snake_case)</param>
public sealed record SourceRowDto(string Key, int Leads, int Won, int? Rate);

/// <param name="Key">Hr.Employee</param>
public sealed record SaleRowDto(Guid Key, string Label, int Leads, int Won, int Open, int? Rate);

/// <param name="Label">Mã chiến dịch</param>
/// <param name="Cpl">Chi phí / lead</param>
/// <param name="Cpa">Chi phí / HV chốt</param>
public sealed record CampaignRowDto(Guid Key, string Label, string Name, decimal Spent, int Leads, int Won, decimal? Cpl, decimal? Cpa);

/// <summary>BC-04</summary>
/// <param name="Growth">% thực thu tháng này so với tháng trước (1 chữ số thập phân); null khi tháng trước = 0</param>
/// <param name="Booked">Doanh số ký trong kỳ</param>
/// <param name="Collected">Thực thu cả kỳ</param>
public sealed record MoneyReportDto(IReadOnlyList<MonthPointDto> Revenue, decimal ThisMonth, decimal PrevMonth, decimal? Growth, decimal Booked,
    decimal Collected, decimal Debt, decimal Overdue, IReadOnlyList<AgingRowDto> Aging);

/// <param name="Key">not_due · d30 · d60 · d60p (khóa <c>AGING</c> của frontend)</param>
public sealed record AgingRowDto(string Key, int Count, decimal Amount);

/// <summary>BC-05</summary>
/// <param name="Ongoing">Lớp đang học</param>
/// <param name="Enrolling">Lớp chờ khai giảng (đang tuyển sinh)</param>
/// <param name="RoomUseAvg">Công suất TB các phòng tuần này</param>
public sealed record StudyReportDto(int Studying, int Ongoing, int Enrolling, IReadOnlyList<MonthPointDto> NewEnroll,
    IReadOnlyList<MonthPointDto> AttendanceByMonth, IReadOnlyList<FillRowDto> Fill, IReadOnlyList<RoomUseRowDto> RoomUse, int? RoomUseAvg);

/// <param name="Label">Mã lớp</param>
/// <param name="Course">Tên khóa học</param>
/// <param name="Enrolled">HV đang học + bảo lưu</param>
/// <param name="Status">enrolling · ongoing</param>
public sealed record FillRowDto(Guid Key, string Label, string Course, int Enrolled, int Max, int Rate, string Status);

public sealed record RoomUseRowDto(Guid Key, string Label, Guid BranchId, decimal Hours, int Rate);

/// <summary>BC-06 — 1 chi nhánh</summary>
/// <param name="Conv">Tỷ lệ chốt</param>
/// <param name="ThisMonth">Thực thu tháng này</param>
/// <param name="NeedsAttention">BC-06.E1: chuyên cần &lt; 85% hoặc còn nợ quá hạn</param>
public sealed record BranchRowDto(Guid Key, string Label, int Leads, int? Conv, int Students, int Classes, int? Fill, int? Attendance,
    decimal Collected, decimal ThisMonth, decimal Debt, decimal Overdue, bool NeedsAttention);
