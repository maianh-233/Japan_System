using System.Collections.Concurrent;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseSetting("ConnectionStrings:Database", _pg.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789-abcdef");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DefaultPassword", Password);
        builder.UseSetting("RateLimit:Enabled", "false");
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails)));
    }

    /// <summary>Email "đã gửi" (OTP, link mời...) — test đọc lại thay cho Mailpit.</summary>
    public CapturingEmailSender Emails { get; } = new();

    public AsyncServiceScope Scope() => Services.CreateAsyncScope();
}

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Html)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        Sent.Enqueue((to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public (string To, string Subject, string Html) Last(string to) => Sent.Last(m => m.To == to);
}

[CollectionDefinition(Name)]
public sealed class SeededCollection : ICollectionFixture<SeededApiFactory>
{
    public const string Name = "seeded";
}
