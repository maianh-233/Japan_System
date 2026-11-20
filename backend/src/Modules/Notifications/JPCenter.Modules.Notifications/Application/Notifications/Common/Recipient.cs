using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Domain.Notifications;

namespace JPCenter.Modules.Notifications.Application.Notifications.Common;

/// <summary>Người nhận = tài khoản đang đăng nhập: nhân viên / GV → <c>staff</c> (user_accounts), HV / PH → <c>portal</c> (portal_accounts).</summary>
public readonly record struct Recipient(RecipientType Type, Guid AccountId)
{
    public static Recipient? Of(ICurrentUser user) => user.Access switch
    {
        { Portal: Portal.Staff or Portal.Teacher } a => new Recipient(RecipientType.Staff, a.AccountId),
        { Portal: Portal.Student or Portal.Parent } a => new Recipient(RecipientType.Portal, a.AccountId),
        _ => null,
    };
}
