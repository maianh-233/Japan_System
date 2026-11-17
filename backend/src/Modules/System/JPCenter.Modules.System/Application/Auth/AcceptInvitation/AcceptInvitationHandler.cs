using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Auth.AcceptInvitation;

public sealed class AcceptInvitationHandler(
    ISystemDbContext db, InvitationLookup invitations, ITokenService tokens, IPasswordHasher hasher, IUserAccessProvider accessProvider,
    SessionIssuer sessions, IAuditTrail audit, IClock clock) : ICommandHandler<AcceptInvitationCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(AcceptInvitationCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (await invitations.FindAsync(cmd.Token, ct) is not var (token, account)) return AuthErrors.InviteInvalid;
        if (PasswordRules.LooksLikeLogin(cmd.Password, account.Email)) return AuthErrors.PasswordLikeLogin;
        if (token.Verify(tokens.Hash(cmd.Token), now).IsFailure) return AuthErrors.InviteInvalid;

        account.SetPassword(hasher.Hash(cmd.Password)); // invited → active
        account.RecordLogin(now);
        await db.SaveChangesAsync(ct);

        var access = await accessProvider.LoadAsync(account.Id.Value, false, ct);
        if (access is null || !access.IsActive) return AuthErrors.InviteInvalid;

        ISignInAccount signIn = account;
        var (_, accessToken, accessExpires, refresh) = sessions.Start(signIn, access.Portal, remember: false);
        await db.SaveChangesAsync(ct);

        audit.Record(AuditCategory.Auth, AuditSeverity.Info, "Kích hoạt tài khoản", account.Email, null,
            new AuditActor(account.Id.Value, access.DisplayName, access.RoleName));
        return new SignInResult(SessionUserFactory.Create(access, account.Email, false), accessToken, accessExpires, refresh);
    }
}
