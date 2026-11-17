using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>
/// Gửi (lại) email mời kích hoạt (AUTH-05): link dùng 1 lần, hạn 72 giờ; link cũ vô hiệu ngay (AUTH-05.E2).
/// Dùng khi cấp tài khoản (PQ-08), gửi lại lời mời, và quên mật khẩu khi tài khoản còn "đã mời" (AUTH-04.E13).
/// </summary>
public sealed class InvitationMailer(ISystemDbContext db, ITokenService tokens, IEmployeeDirectory employees, IFrontendLinks links,
    IEmailSender email, IClock clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(72);

    public async Task SendAsync(UserAccount account, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var old = await db.OneTimeTokens
            .Where(x => x.Purpose == TokenPurpose.Invite && x.AccountId == account.Id.Value && x.UsedAt == null && x.InvalidatedAt == null)
            .ToListAsync(ct);
        old.ForEach(t => t.Invalidate(now));

        var secret = tokens.NewSecret();
        db.OneTimeTokens.Add(OneTimeToken.Issue(TokenPurpose.Invite, AccountKind.Staff, account.Id.Value, account.Email,
            tokens.Hash(secret), now, Lifetime, maxAttempts: 1));
        await db.SaveChangesAsync(ct);

        var employee = await employees.FindAsync(account.EmployeeId, ct);
        var roleIds = account.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(ct);
        var (subject, html) = AuthEmails.Invitation(employee?.FullName ?? account.Email,
            roles.Count > 0 ? string.Join(", ", roles) : "chưa gán", links.Activation(secret));
        await email.SendAsync(account.Email, subject, html, ct);
    }
}
