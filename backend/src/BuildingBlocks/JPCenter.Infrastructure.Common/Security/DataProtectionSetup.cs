using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Security;

/// <summary>
/// ASP.NET Data Protection dùng chung (mã hóa khóa bí mật tích hợp, token làm bài…). Khóa lưu BỀN ra thư mục
/// <c>DataProtection:KeysPath</c> (mặc định <c>%LOCALAPPDATA%/JPCenter/dp-keys</c>; Docker: gắn volume) — mất thư mục này = không giải mã được dữ liệu đã mã hóa.
/// Tên ứng dụng cố định để mọi máy / đường dẫn triển khai dùng chung 1 bộ khóa.
/// </summary>
public static class DataProtectionSetup
{
    public const string ApplicationName = "JPCenter";

    public static IServiceCollection AddPersistentDataProtection(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName(ApplicationName);
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((o, configuration, loggers) =>
            o.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(KeysPath(configuration)), loggers));
        return services;
    }

    public static string KeysPath(IConfiguration configuration) =>
        configuration["DataProtection:KeysPath"] is { Length: > 0 } path
            ? path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JPCenter", "dp-keys");
}
