using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.PortalAccounts;

namespace JPCenter.Modules.System.Application.PortalAccounts.Common;

/// <summary>
/// Sinh mật khẩu tạm (bắt đổi lần đầu — AUTH-10 bước 3) và gửi qua email nếu có.
/// Zalo / SMS chưa tích hợp → không có email thì trả mật khẩu tạm cho giáo vụ đưa tận tay.
/// </summary>
public sealed class PortalCredentialSender(ITokenService tokens, IPasswordHasher hasher, IEmailSender email, IFrontendLinks links)
{
    public async Task<PortalCredentialDto> IssueAsync(PortalAccount account, CancellationToken ct, Func<Task>? save = null)
    {
        var password = tokens.NewTemporaryPassword();
        account.SetTemporaryPassword(hasher.Hash(password));
        if (save is not null) await save();

        if (account.Email is not { } to) return new PortalCredentialDto(account.ToDto(), password, false);

        var (subject, html) = AuthEmails.TemporaryPassword(account.DisplayName, account.Username, password, links.Login());
        await email.SendAsync(to, subject, html, ct);
        return new PortalCredentialDto(account.ToDto(), null, true);
    }
}
