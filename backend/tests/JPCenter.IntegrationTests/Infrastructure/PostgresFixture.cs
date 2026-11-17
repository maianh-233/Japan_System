using Testcontainers.PostgreSql;

namespace JPCenter.IntegrationTests.Infrastructure;

/// <summary>PostgreSQL thật trong Docker (Testcontainers) — tạo 1 lần cho cả nhóm test, xóa khi xong. Không đụng DB dữ liệu mẫu.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
