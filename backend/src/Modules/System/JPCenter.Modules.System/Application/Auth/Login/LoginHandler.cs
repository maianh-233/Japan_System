using JPCenter.Application.Common.Email;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.Login;

/// <summary>
/// AUTH-01 + AUTH-11. Thứ tự kiểm tra cố định:
/// khóa tạm (không so mật khẩu) → so mật khẩu (sai / không tồn tại cùng 1 thông báo) → trạng thái tài khoản → cấp phiên.
/// </summary>
public sealed class LoginHandler(
    ISystemDbContext db, SignInAccounts accounts, IPasswordHasher hasher, IUserAccessProvider accessProvider,
    SessionIssuer sessions, IAuditTrail audit, IEmailSender email, IClock clock, IRequestInfo request) : ICommandHandler<LoginCommand, SignInResult>
{
    public const string LockedAction = "Tạm khóa do nhập sai mật khẩu";
    private static string? _dummyHash;

    public async Task<Result<SignInResult>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var login = SignInAccounts.Normalize(cmd.Identifier);
        var account = await accounts.FindByLoginAsync(login, ct);

        if (account is null)
        {
            // Vẫn băm để thời gian phản hồi như tài khoản có thật — không dò được email nào tồn tại
            hasher.Verify(_dummyHash ??= hasher.Hash("jpcenter-dummy-password"), cmd.Password);
            audit.Record(AuditCategory.Auth, AuditSeverity.Warning, "Đăng nhập thất bại", login, "Tài khoản không tồn tại",
                new AuditActor(null, "không xác định", "—"));
            return AuthErrors.InvalidCredentials;
        }

        // AUTH-11.E1: đang khóa tạm → không kiểm tra mật khẩu
        if (account.CheckThrottle(now) is { IsFailure: true } locked) return locked.Error;

        // AUTH-01.E3: tài khoản được mời chưa đặt mật khẩu — không thể "nhập đúng", báo chờ kích hoạt để người dùng mở email mời
        if (account.PasswordHash is null && account.CheckStatus() is { IsFailure: true } pending) return pending.Error;

        if (account.PasswordHash is null || !hasher.Verify(account.PasswordHash, cmd.Password))
        {
            var justLocked = account.RecordFailedLogin(now);
            await db.SaveChangesAsync(ct);
            var actor = new AuditActor(account.AccountGuid, login, "—");
            if (justLocked) await OnLockedAsync(account, login, actor, now, ct);
            else audit.Record(AuditCategory.Auth, AuditSeverity.Warning, "Đăng nhập thất bại", login, "Sai mật khẩu", actor);
            return AuthErrors.InvalidCredentials;
        }

        // AUTH-01.E2 – E4: chỉ báo trạng thái khi mật khẩu ĐÚNG
        if (account.CheckStatus() is { IsFailure: true } status) return status.Error;

        var access = await accessProvider.LoadAsync(account.AccountGuid, account.Kind == AccountKind.Portal, ct);
        // AUTH-01.E6: nhân viên đã nghỉ việc → như bị khóa
        if (access is null || !access.IsActive) return UserAccountErrors.AccountLocked;

        account.RecordLogin(now);
        var (_, accessToken, accessExpires, refresh) = sessions.Start(account, access.Portal, cmd.Remember);
        await db.SaveChangesAsync(ct);

        audit.Record(AuditCategory.Auth, AuditSeverity.Info, "Đăng nhập", login, null,
            new AuditActor(account.AccountGuid, access.DisplayName, access.RoleName));

        var user = SessionUserFactory.Create(access, account.Email ?? login, account.MustChangePassword);
        return new SignInResult(user, accessToken, accessExpires, refresh);
    }

    /// <summary>AUTH-11: vừa bị khóa tạm. Lần thứ 3 trong 24 giờ → email cảnh báo chủ tài khoản + nhật ký critical (AUTH-11.E2).</summary>
    private async Task OnLockedAsync(ISignInAccount account, string login, AuditActor actor, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddHours(-24);
        var times = 1 + await db.AuditLogs.CountAsync(x => x.Target == login && x.Action == LockedAction && x.At > since, ct);
        audit.Record(AuditCategory.Auth, times >= 3 ? AuditSeverity.Critical : AuditSeverity.Warning, LockedAction, login,
            $"Sai mật khẩu {LoginThrottle.MaxFailures} lần liên tiếp — khóa {LoginThrottle.LockMinutes} phút (lần {times} trong 24 giờ)", actor);

        if (times >= 3 && account.Email is { } to)
        {
            await email.SendAsync(to, AuthEmails.LockoutWarning(times, clock.UtcNow, request.Device, request.Ip), ct);
        }
    }
}
