using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IIntegrationSecrets"/> bằng ASP.NET Data Protection (khóa lưu bền — <c>DataProtectionSetup</c>).</summary>
internal sealed class IntegrationSecrets(IDataProtectionProvider provider) : IIntegrationSecrets
{
    private const string Prefix = "enc:";
    private readonly IDataProtector _protector = provider.CreateProtector("JPCenter.System.Integrations.Secrets.v1");

    public string Protect(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) ? value : Prefix + _protector.Protect(value);

    public string Reveal(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) ? _protector.Unprotect(value[Prefix.Length..]) : value;
}
