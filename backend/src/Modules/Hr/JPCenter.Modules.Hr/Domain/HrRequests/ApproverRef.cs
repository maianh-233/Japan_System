namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Người duyệt dự kiến của 1 bước (null = bước mở)</summary>
public sealed record ApproverRef(Guid Id, string Name);
