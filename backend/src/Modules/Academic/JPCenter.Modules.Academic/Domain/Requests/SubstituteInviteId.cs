using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>Id lời mời dạy thay</summary>
public readonly record struct SubstituteInviteId(Guid Value)
{
    public static SubstituteInviteId New() => new(IdGenerator.NewId());
}
