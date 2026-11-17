using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Auth.ChangePassword;

/// <summary>AUTH-06: sai mật khẩu hiện tại tính vào bộ đếm sai (E1); mới phải khác cũ (E2); chọn đăng xuất thiết bị khác (E3).</summary>
public sealed class ChangePasswordHandler(
    ISystemDbContext db, ICurrentUser user, SignInAccounts accounts, IPasswordHasher hasher, SessionIssuer sessions,
    SessionRevoker revoker, IUserAccessCache accessCache, IEmailSender email, IAuditTrail audit, IClock clock)
    : ICommandHandler<ChangePasswordCommand, ChangePasswordResult>
{
    public async Task<Result<ChangePasswordResult>> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (user.Access is not { } access || user.SessionId is not { } sessionId) return AuthErrors.RefreshInvalid;
        var kind = access.Portal is Portal.Student or Portal.Parent ? AccountKind.Portal : AccountKind.Staff;
        var account = await accounts.FindAsync(kind, access.AccountId, ct);
        if (account is null) return AuthErrors.RefreshInvalid;

        if (account.CheckThrottle(now) is { IsFailure: true } locked) return locked.Error;
        if (account.PasswordHash is null || !hasher.Verify(account.PasswordHash, cmd.CurrentPassword))
        {
            account.RecordFailedLogin(now);
            await db.SaveChangesAsync(ct);
            return AuthErrors.WrongPassword;
        }
        if (cmd.CurrentPassword == cmd.NewPassword) return AuthErrors.SamePassword;
        if (PasswordRules.LooksLikeLogin(cmd.NewPassword, account.LoginName)) return AuthErrors.PasswordLikeLogin;

        account.SetPassword(hasher.Hash(cmd.NewPassword));
        var loggedOut = cmd.LogoutOtherDevices
            ? await revoker.RevokeAllAsync(kind, account.AccountGuid, "logout_others", sessionId, ct)
            : 0;
        await db.SaveChangesAsync(ct);
        accessCache.Invalidate(account.AccountGuid);

        if (account.Email is { } to)
        {
            var (subject, html) = AuthEmails.PasswordChanged(now);
            await email.SendAsync(to, subject, html, ct);
        }
        audit.Record(AuditCategory.Auth, AuditSeverity.Warning, "Đổi mật khẩu", account.LoginName,
            loggedOut > 0 ? $"Đăng xuất {loggedOut} thiết bị khác" : null);

        var (token, expires) = sessions.Reissue(account, access.Portal, sessionId);
        return new ChangePasswordResult(token, expires, loggedOut);
    }
}
