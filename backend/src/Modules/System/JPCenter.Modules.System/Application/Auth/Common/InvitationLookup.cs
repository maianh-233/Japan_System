using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>Tìm lời mời còn hiệu lực: chưa dùng, chưa bị thay, chưa hết hạn, tài khoản còn "đã mời" (AUTH-05.E1, E3).</summary>
public sealed class InvitationLookup(ISystemDbContext db, ITokenService tokens, IClock clock)
{
    public async Task<(OneTimeToken Token, UserAccount Account)?> FindAsync(string rawToken, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.Hash(rawToken);
        var token = await db.OneTimeTokens.FirstOrDefaultAsync(x => x.Purpose == TokenPurpose.Invite && x.TokenHash == hash, ct);
        if (token?.AccountId is null || token.UsedAt is not null || token.InvalidatedAt is not null || token.ExpiresAt <= now) return null;

        var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Id == new UserAccountId(token.AccountId.Value), ct);
        return account is { Status: UserAccountStatus.Invited } ? (token, account) : null;
    }
}
