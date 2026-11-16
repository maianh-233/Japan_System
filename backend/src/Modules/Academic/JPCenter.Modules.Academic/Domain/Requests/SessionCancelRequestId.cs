using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>Id đơn hủy buổi của GV</summary>
public readonly record struct SessionCancelRequestId(Guid Value)
{
    public static SessionCancelRequestId New() => new(IdGenerator.NewId());
}
