using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Link về frontend trong email — gốc lấy từ <c>App:FrontendUrl</c> (mặc định http://localhost:5173).</summary>
internal sealed class FrontendLinks(IConfiguration configuration) : IFrontendLinks
{
    private string Base => (configuration["App:FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/');

    public string Activation(string token) => $"{Base}/activate?token={Uri.EscapeDataString(token)}";

    public string Login() => $"{Base}/login";
}
