using JPCenter.Application.Common.Email;
using JPCenter.Modules.System.Application.Abstractions;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Link về frontend trong email của System — ghép đường dẫn qua <see cref="IAppLinks"/> dùng chung (<c>App:FrontendUrl</c>).</summary>
internal sealed class FrontendLinks(IAppLinks links) : IFrontendLinks
{
    public string Activation(string token) => links.Page($"/activate?token={Uri.EscapeDataString(token)}");

    public string Login() => links.Page("/login");

    public string Page(string path) => links.Page(path);
}
