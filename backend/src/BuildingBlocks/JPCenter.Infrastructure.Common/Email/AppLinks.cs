using JPCenter.Application.Common.Email;

namespace JPCenter.Infrastructure.Common.Email;

/// <summary>Hiện thực <see cref="IAppLinks"/> từ <c>App:FrontendUrl</c>.</summary>
public sealed class AppLinks(EmailOptions options) : IAppLinks
{
    public string Page(string path) => $"{options.WebsiteUrl}/{path.TrimStart('/')}";
}
