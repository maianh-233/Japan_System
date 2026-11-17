using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.ResetPassword;

/// <summary>
/// AUTH-04 bước 3: đặt mật khẩu → xóa khóa tạm, bỏ cờ bắt đổi (E11), thu hồi mọi phiên, email báo "Mật khẩu vừa được đổi", nhật ký.
/// </summary>
public sealed class ResetPasswordHandler(
    ISystemDbContext db, SignInAccounts accounts, ITokenService tokens, IPasswordHasher hasher, SessionRevoker revoker,
    IUserAccessCache accessCache, IEmailSender email, IAuditTrail audit, IClock clock) : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.Hash(cmd.ResetToken);
        var grant = await db.OneTimeTokens.FirstOrDefaultAsync(x => x.Purpose == TokenPurpose.PasswordResetGrant && x.TokenHash == hash, ct);
        // AUTH-04.E7: chưa xác thực OTP / vé hết hạn / đã dùng
        if (grant?.AccountId is null || grant.Verify(hash, now).IsFailure) return AuthErrors.OtpRequired;

        var account = await accounts.FindAsync(grant.AccountKind, grant.AccountId.Value, ct);
        if (account is null) return AuthErrors.OtpRequired;
        if (PasswordRules.LooksLikeLogin(cmd.NewPassword, account.LoginName)) return AuthErrors.PasswordLikeLogin;

        account.SetPassword(hasher.Hash(cmd.NewPassword));
        await revoker.RevokeAllAsync(account.Kind, account.AccountGuid, "password_changed", null, ct);
        await db.SaveChangesAsync(ct);
        accessCache.Invalidate(account.AccountGuid);

        if (account.Email is { } to)
        {
            var (subject, html) = AuthEmails.PasswordChanged(now);
            await email.SendAsync(to, subject, html, ct);
        }
        audit.Record(AuditCategory.Auth, AuditSeverity.Warning, "Đặt lại mật khẩu (quên mật khẩu)", account.LoginName,
            actor: new AuditActor(account.AccountGuid, account.LoginName, "—"));
        return Result.Success();
    }
}
