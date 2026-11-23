namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Người đang giữ các chức vụ duyệt (tìm theo vị trí trong hồ sơ, bỏ người đã nghỉ) — null = chưa có ai</summary>
public sealed record ApproverHolders(ApproverRef? Deputy, ApproverRef? ChiefAccountant, ApproverRef? Director);
