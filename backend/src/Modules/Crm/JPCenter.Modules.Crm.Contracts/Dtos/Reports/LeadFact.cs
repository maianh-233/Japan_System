namespace JPCenter.Modules.Crm.Contracts.Dtos.Reports;

/// <summary>1 lead cho báo cáo tuyển sinh (BC-03) — không có thông tin liên hệ.</summary>
/// <param name="Status">new · consulting · test_scheduled · won · lost</param>
/// <param name="Source">facebook · website · zalo · tiktok · referral · walk_in · hotline · event</param>
/// <param name="AssigneeId">Hr.Employee — Sale phụ trách</param>
public sealed record LeadFact(Guid Id, Guid BranchId, DateTimeOffset CreatedAt, string Status, string Source, Guid? AssigneeId);
