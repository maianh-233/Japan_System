using System.Collections.Concurrent;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Application.Common.Email;
using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.IntegrationTests.BuildingBlocks;
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
    public const int OutboxMaxAttempts = 3;
    /// <summary>Khóa ký webhook ngân hàng (TC-10.E6)</summary>
    public const string BankWebhookSecret = "test-bank-webhook-secret";
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
        builder.UseSetting("Finance:BankWebhookSecret", BankWebhookSecret);
        // Outbox chạy nhanh để test không phải chờ lâu
        builder.UseSetting("Outbox:PollMilliseconds", "200");
        builder.UseSetting("Outbox:RetryBaseMilliseconds", "50");
        builder.UseSetting("Outbox:MaxAttempts", OutboxMaxAttempts.ToString());
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails));
            services.Replace(ServiceDescriptor.Singleton<IFileStorage>(Files));
            // Handler sự kiện chỉ có trong test (BuildingBlocks/OutboxTests.cs)
            services.AddScoped<IIntegrationEventHandler<OutboxPing>, OutboxPingOkHandler>();
            services.AddScoped<IIntegrationEventHandler<OutboxPing>, OutboxPingFlakyHandler>();
        });
    }

    /// <summary>Kho file giả — tài liệu lộ trình, file mẫu của seeder.</summary>
    public InMemoryFileStorage Files { get; } = new();

    /// <summary>Email "đã gửi" (OTP, link mời...) — test đọc lại thay cho Mailpit.</summary>
    public CapturingEmailSender Emails { get; } = new();

    public AsyncServiceScope Scope() => Services.CreateAsyncScope();
}

/// <summary>Ghi lại email "đã gửi" — <c>Text</c> là bản chữ thuần (đọc mã OTP, link mời trong test).</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Text)> Sent { get; } = new();

    public Task SendAsync(string to, EmailContent content, CancellationToken ct)
    {
        Sent.Enqueue((to, content.Subject, content.ToPlainText()));
        return Task.CompletedTask;
    }

    public (string To, string Subject, string Text) Last(string to) => Sent.Last(m => m.To == to);
}

[CollectionDefinition(Name)]
public sealed class SeededCollection : ICollectionFixture<SeededApiFactory>
{
    public const string Name = "seeded";
}
