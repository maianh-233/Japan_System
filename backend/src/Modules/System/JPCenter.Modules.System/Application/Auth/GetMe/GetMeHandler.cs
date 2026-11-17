using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Auth.GetMe;

public sealed class GetMeHandler(ICurrentUser user, SignInAccounts accounts) : IQueryHandler<GetMeQuery, SessionUserDto>
{
    public async Task<Result<SessionUserDto>> Handle(GetMeQuery query, CancellationToken ct)
    {
        if (user.Access is not { } access) return AuthErrors.RefreshInvalid;
        var kind = access.Portal is Portal.Student or Portal.Parent ? AccountKind.Portal : AccountKind.Staff;
        var account = await accounts.FindAsync(kind, access.AccountId, ct);
        if (account is null) return AuthErrors.RefreshInvalid;
        return SessionUserFactory.Create(access, account.Email ?? account.LoginName, account.MustChangePassword);
    }
}
