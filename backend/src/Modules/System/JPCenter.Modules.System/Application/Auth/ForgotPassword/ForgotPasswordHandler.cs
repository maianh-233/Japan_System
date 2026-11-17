using JPCenter.Application.Common.Email;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.ForgotPassword;

/// <summary>
/// AUTH-04 bước 1. Chống dò email (E1): email không tồn tại / bị khóa vẫn tạo mã "giả" (không gửi, mã nào cũng sai) và trả
/// kết quả giống hệt. Giới hạn: gửi lại sau 60 giây, tối đa 5 lần / giờ / email (E6). Mã cũ vô hiệu khi gửi mã mới.
/// </summary>
public sealed class ForgotPasswordHandler(
    ISystemDbContext db, SignInAccounts accounts, ITokenService tokens, InvitationMailer invitations, IEmailSender email,
    IAuditTrail audit, IClock clock) : ICommandHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    public const int ResendAfterSeconds = 60;
    public const int MaxPerHour = 5;
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<ForgotPasswordResult>> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var recipient = SignInAccounts.Normalize(cmd.Email);
        var hourAgo = now.AddHours(-1);

        var recent = await db.OneTimeTokens
            .Where(x => x.Recipient == recipient && x.Purpose == TokenPurpose.PasswordResetOtp && x.CreatedAt > hourAgo)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        if (recent.Count >= MaxPerHour) return AuthErrors.OtpRateLimited;
        if (recent.FirstOrDefault() is { } last && now - last.CreatedAt < TimeSpan.FromSeconds(ResendAfterSeconds))
            return AuthErrors.ResendTooSoon(ResendAfterSeconds - (int)(now - last.CreatedAt).TotalSeconds);
        recent.ForEach(t => t.Invalidate(now));

        var account = await accounts.FindByEmailAsync(recipient, ct);
        var canReceive = account is not null && account.CheckStatus().IsSuccess;

        var otp = tokens.NewOtp();
        db.OneTimeTokens.Add(OneTimeToken.Issue(TokenPurpose.PasswordResetOtp, account?.Kind ?? AccountKind.Staff,
            canReceive ? account!.AccountGuid : null, recipient, tokens.Hash(otp), now, OtpLifetime));
        await db.SaveChangesAsync(ct);

        if (canReceive)
        {
            await email.SendAsync(recipient, AuthEmails.ResetOtp(otp), ct);
        }
        else if (account is UserAccount { Status: UserAccountStatus.Invited } invited)
        {
            // AUTH-04.E13: chưa kích hoạt → gửi lại email mời thay cho OTP
            await invitations.SendAsync(invited, ct);
        }
        else if (account is not null)
        {
            // AUTH-04.E2: tài khoản bị khóa — không gửi, nhưng để lại dấu vết
            audit.Record(AuditCategory.Auth, AuditSeverity.Warning, "Yêu cầu đặt lại mật khẩu khi tài khoản bị khóa", recipient,
                actor: new AuditActor(account.AccountGuid, recipient, "—"));
        }

        return new ForgotPasswordResult(Mask(recipient), ResendAfterSeconds);
    }

    /// <summary>"nguyenvan@gmail.com" → "ng******@gmail.com"</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return email;
        var name = email[..at];
        return $"{name[..Math.Min(2, name.Length)]}{new string('*', Math.Max(1, name.Length - 2))}{email[at..]}";
    }
}
