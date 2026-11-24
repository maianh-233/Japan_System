namespace JPCenter.Modules.Academic.Contracts.Dtos.Reports;

/// <summary>Lớp đang học / đang tuyển sinh cho báo cáo lấp đầy (BC-05 bước 4, BC-06).</summary>
/// <param name="Status">enrolling · ongoing</param>
public sealed record ClassFact(Guid Id, string Code, Guid BranchId, string Status, int MaxStudents, string CourseName);
