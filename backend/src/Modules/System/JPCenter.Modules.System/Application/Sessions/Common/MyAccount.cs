using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Domain.Sessions;

namespace JPCenter.Modules.System.Application.Sessions.Common;

/// <summary>Loại + Id tài khoản của người đang gọi (trang Cá nhân).</summary>
public static class MyAccount
{
    public static (AccountKind Kind, Guid Id)? Of(ICurrentUser user) => user.Access is { } a
        ? (a.Portal is Portal.Student or Portal.Parent ? AccountKind.Portal : AccountKind.Staff, a.AccountId)
        : null;
}
