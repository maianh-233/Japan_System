using System.Security.Cryptography;
using System.Text.Json;
using JPCenter.Modules.Academic.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IQuizTokens"/>: JSON → ASP.NET Data Protection (ký + mã hóa, hạn 1 ngày)</summary>
internal sealed class QuizTokens(IDataProtectionProvider provider) : IQuizTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);

    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector("academic.quiz-ticket.v1").ToTimeLimitedDataProtector();

    public string Issue(QuizTicket ticket) => _protector.Protect(JsonSerializer.Serialize(ticket), Lifetime);

    public QuizTicket? Read(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<QuizTicket>(_protector.Unprotect(token));
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
