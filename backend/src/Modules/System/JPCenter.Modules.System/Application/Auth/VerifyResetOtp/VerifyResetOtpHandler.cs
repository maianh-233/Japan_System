using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.VerifyResetOtp;

/// <summary>AUTH-04 bước 2: hạn 5 phút (E5), tối đa 5 lần sai (E3, E4). Mã "giả" (email không tồn tại) không bao giờ đúng.</summary>
public sealed class VerifyResetOtpHandler(ISystemDbContext db, ITokenService tokens, IClock clock)
    : ICommandHandler<VerifyResetOtpCommand, VerifyResetOtpResult>
{
    public static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(10);

    public async Task<Result<VerifyResetOtpResult>> Handle(VerifyResetOtpCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var recipient = SignInAccounts.Normalize(cmd.Email);
        var token = await db.OneTimeTokens
            .Where(x => x.Recipient == recipient && x.Purpose == TokenPurpose.PasswordResetOtp && x.UsedAt == null && x.InvalidatedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (token is null) return OneTimeTokenErrors.Expired;

        var verified = token.Verify(tokens.Hash(cmd.Otp), now);
        if (verified.IsFailure)
        {
            await db.SaveChangesAsync(ct); // lưu số lần sai
            return verified.Error;
        }

        var grant = tokens.NewSecret();
        db.OneTimeTokens.Add(OneTimeToken.Issue(TokenPurpose.PasswordResetGrant, token.AccountKind, token.AccountId, recipient,
            tokens.Hash(grant), now, GrantLifetime, maxAttempts: 1));
        await db.SaveChangesAsync(ct);
        return new VerifyResetOtpResult(grant);
    }
}
