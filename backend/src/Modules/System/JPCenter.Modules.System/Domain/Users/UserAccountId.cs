using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Users;

/// <summary>Id tài khoản nhân viên (đăng nhập cổng nhân viên / giáo viên)</summary>
public readonly record struct UserAccountId(Guid Value)
{
    public static UserAccountId New() => new(IdGenerator.NewId());
}
