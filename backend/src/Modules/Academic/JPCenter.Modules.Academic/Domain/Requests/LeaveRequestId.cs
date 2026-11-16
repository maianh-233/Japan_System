using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>Id đơn xin nghỉ của HV / PH</summary>
public readonly record struct LeaveRequestId(Guid Value)
{
    public static LeaveRequestId New() => new(IdGenerator.NewId());
}
