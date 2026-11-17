using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>
/// Chạy lúc khởi động (Development): áp migration của mọi DbContext module, rồi nạp dữ liệu mẫu theo <see cref="IDataSeeder.Order"/>.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider root, CancellationToken ct = default)
    {
        await using var scope = root.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        foreach (var registration in sp.GetServices<ModuleDbContextRegistration>())
        {
            var db = (DbContext)sp.GetRequiredService(registration.ContextType);
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count == 0) continue;
            logger.LogInformation("Áp {Count} migration cho {Context}: {Migrations}", pending.Count, registration.ContextType.Name, pending);
            await db.Database.MigrateAsync(ct);
        }

        if (!sp.GetRequiredService<IConfiguration>().GetValue<bool>("Seed:Enabled")) return;
        foreach (var seeder in sp.GetServices<IDataSeeder>().OrderBy(s => s.Order))
        {
            logger.LogInformation("Nạp dữ liệu mẫu: {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(ct);
        }
    }
}

/// <summary>Ghi nhận 1 DbContext module để <see cref="DatabaseInitializer"/> chạy migration.</summary>
public sealed record ModuleDbContextRegistration(Type ContextType);
