using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Approvals;

/// <summary>Id yêu cầu duyệt 2 người</summary>
public readonly record struct ApprovalRequestId(Guid Value)
{
    public static ApprovalRequestId New() => new(IdGenerator.NewId());
}
