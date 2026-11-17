using System.Collections.Concurrent;
using System.Net.Http.Headers;
using JPCenter.Api.Common.Modules;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Idempotency;
using JPCenter.Infrastructure.Common.Idempotency;
using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JPCenter.IntegrationTests.Middleware;

/// <summary>Chạy API thật trong bộ nhớ, thêm module "test" + nguồn quyền giả + nơi ghi nhật ký giả.</summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-test-signing-key-0123456789-abcdef";

    static TestApiFactory() => Environment.SetEnvironmentVariable("JPCENTER_SKIP_DOTENV", "1");

    public FakeAccessProvider Accounts { get; } = new();
    public CapturingAuditSink Audit { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", "jpcenter-api");
        builder.UseSetting("Jwt:Audience", "jpcenter-web");
        builder.UseSetting("Cors:AllowedOrigins", "http://localhost:5173");
        // Bộ test middleware KHÔNG dùng DB: chuỗi kết nối giả (không bao giờ mở), mọi thứ cần lưu trữ thay bằng bản trong bộ nhớ
        builder.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Database=unused");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IModule>(new TestModule());
            services.Replace(ServiceDescriptor.Singleton<IUserAccessProvider>(Accounts));
            services.Replace(ServiceDescriptor.Singleton<IAuditSink>(Audit));
            services.Replace(ServiceDescriptor.Singleton<IIdempotencyStore, InMemoryIdempotencyStore>());
        });
    }

    /// <summary>Tạo tài khoản giả với các mã quyền đầy đủ ("test.items.view"...) và trả client đã gắn token.</summary>
    public HttpClient ClientFor(params string[] permissions)
    {
        var access = Accounts.Add(permissions);
        var (token, _) = Services.GetRequiredService<AccessTokenIssuer>().Issue(access.AccountId, access.Portal, access.TokenVersion, Guid.NewGuid());
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class FakeAccessProvider : IUserAccessProvider
{
    private readonly ConcurrentDictionary<Guid, UserAccess> _accounts = new();

    public UserAccess Add(params string[] permissions)
    {
        var id = Guid.NewGuid();
        var access = new UserAccess(id, Portal.Staff, 1, true, $"Người thử {id.ToString()[..4]}", "Giáo vụ", Guid.NewGuid(), null,
            false, false, [], permissions.ToDictionary(p => p, _ => new EffectivePermission(PermissionScope.Branch, [])));
        _accounts[id] = access;
        return access;
    }

    public void Update(Guid id, Func<UserAccess, UserAccess> change) => _accounts[id] = change(_accounts[id]);

    public Task<UserAccess?> LoadAsync(Guid accountId, bool isPortalAccount, CancellationToken ct) =>
        Task.FromResult(_accounts.GetValueOrDefault(accountId));
}

public sealed class CapturingAuditSink : IAuditSink
{
    public ConcurrentQueue<AuditEntry> Entries { get; } = new();

    public Task WriteAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct)
    {
        foreach (var e in entries) Entries.Enqueue(e);
        return Task.CompletedTask;
    }

    /// <summary>Nhật ký ghi nền — chờ tối đa 3 giây.</summary>
    public async Task<AuditEntry> WaitForAsync(Func<AuditEntry, bool> match)
    {
        for (var i = 0; i < 60; i++)
        {
            var hit = Entries.FirstOrDefault(match);
            if (hit is not null) return hit;
            await Task.Delay(50);
        }
        throw new TimeoutException("Không thấy dòng nhật ký mong đợi");
    }
}
