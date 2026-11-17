using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>PBKDF2-HMAC-SHA512, 100.000 vòng, muối ngẫu nhiên — <c>PasswordHasher</c> có sẵn của ASP.NET Core (ADR 0002).</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Inner = new();
    private static readonly object User = new();

    public string Hash(string password) => Inner.HashPassword(User, password);

    public bool Verify(string hash, string password) =>
        Inner.VerifyHashedPassword(User, hash, password) is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
