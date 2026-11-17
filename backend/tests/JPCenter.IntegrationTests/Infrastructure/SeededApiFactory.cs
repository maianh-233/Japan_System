using JPCenter.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Infrastructure;

/// <summary>
/// API thật + PostgreSQL thật (Testcontainers) đã chạy migration và nạp dữ liệu mẫu giống môi trường dev.
/// Dùng chung 1 lần cho cả nhóm test (collection "seeded").
/// </summary>
public sealed class SeededApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Test@12345";
    private readonly PostgresFixture _pg = new();

    static SeededApiFactory() => Environment.SetEnvironmentVariable("JPCENTER_SKIP_DOTENV", "1");

    public async ValueTask InitializeAsync()
    {
        await _pg.InitializeAsync();
        await Services.InitializeDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _pg.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _pg.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789-abcdef");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DefaultPassword", Password);
        builder.UseSetting("RateLimit:Enabled", "false");
    }

    public AsyncServiceScope Scope() => Services.CreateAsyncScope();
}

[CollectionDefinition(Name)]
public sealed class SeededCollection : ICollectionFixture<SeededApiFactory>
{
    public const string Name = "seeded";
}
