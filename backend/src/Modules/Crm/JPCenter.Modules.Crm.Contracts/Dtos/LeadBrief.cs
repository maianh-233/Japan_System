namespace JPCenter.Modules.Crm.Contracts.Dtos;

/// <summary>Lead rút gọn. <paramref name="Status"/>: new · consulting · test_scheduled · won · lost. <paramref name="AssigneeId"/>: Hr.Employee</summary>
public sealed record LeadBrief(Guid Id, string Code, string FullName, string Phone, Guid BranchId, string Status, Guid? AssigneeId, bool IsOpen);
