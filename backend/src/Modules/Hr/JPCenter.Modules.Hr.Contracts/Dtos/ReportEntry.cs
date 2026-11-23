namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>1 cấp dưới theo cây "quản lý trực tiếp". <paramref name="Level"/>: 1 = trực tiếp, 2+ = gián tiếp</summary>
public sealed record ReportEntry(EmployeeSummary Employee, int Level);
