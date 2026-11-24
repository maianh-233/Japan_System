using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Seed;

/// <summary>
/// Giai đoạn 22 phase 2 — mã hóa khóa bí mật tích hợp còn lưu chữ thường (dữ liệu mẫu cũ / vừa nạp ở <see cref="SystemSeeder"/>).
/// Chạy mỗi lần khởi động, không làm gì khi mọi khóa đã mã hóa; không xóa dữ liệu.
/// </summary>
internal sealed class IntegrationSecretsSeeder(SystemDbContext db, IIntegrationSecrets secrets, ILogger<IntegrationSecretsSeeder> logger) : IDataSeeder
{
    public int Order => 21;

    public async Task SeedAsync(CancellationToken ct)
    {
        var changed = 0;
        foreach (var integration in await db.Integrations.ToListAsync(ct))
            if (integration.RewriteSecrets(secrets.Protect)) changed++;
        if (changed == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã mã hóa khóa bí mật của {Count} tích hợp", changed);
    }
}
