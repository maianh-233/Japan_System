using JPCenter.Application.Common.Security;
using JPCenter.Infrastructure.Common.Time;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JPCenter.Infrastructure.Common.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Dịch vụ hạ tầng chung: đồng hồ, interceptor, người dùng mặc định (= hệ thống, khi ngoài HTTP).</summary>
    public static IServiceCollection AddInfrastructureCommon(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.TryAddScoped<AuditFieldsInterceptor>();
        return services;
    }

    /// <summary>
    /// Đăng ký DbContext của 1 module: PostgreSQL (chuỗi kết nối <c>ConnectionStrings:Database</c>), snake_case,
    /// bảng lịch sử migration nằm trong schema của module, interceptor chung.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : ModuleDbContextBase
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Database (xem .env.example)");

        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditFieldsInterceptor>()));

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        return services;
    }

    /// <summary>Ngoài HTTP request: không có người dùng — created_by / updated_by để trống (= hệ thống).</summary>
    private sealed class SystemCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? AccountId => null;
    }
}
