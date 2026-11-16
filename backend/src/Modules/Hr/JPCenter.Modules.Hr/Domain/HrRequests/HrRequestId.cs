using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Id đơn từ nhân sự</summary>
public readonly record struct HrRequestId(Guid Value)
{
    public static HrRequestId New() => new(IdGenerator.NewId());
}
