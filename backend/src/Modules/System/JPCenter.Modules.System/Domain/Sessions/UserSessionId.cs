using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Sessions;

/// <summary>Id phiên đăng nhập (1 thiết bị)</summary>
public readonly record struct UserSessionId(Guid Value)
{
    public static UserSessionId New() => new(IdGenerator.NewId());
}
