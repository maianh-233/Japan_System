using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.PortalAccounts;

/// <summary>Id tài khoản cổng học viên / phụ huynh</summary>
public readonly record struct PortalAccountId(Guid Value)
{
    public static PortalAccountId New() => new(IdGenerator.NewId());
}
