namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Bước duyệt đã dựng, chưa gắn vào đơn</summary>
public sealed record PlannedStep(string Role, ApproverRef? Approver);
