using JPCenter.Application.Common.Email;
using JPCenter.Infrastructure.Common.Email;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Idempotency;
using JPCenter.Application.Common.Security;
using JPCenter.Infrastructure.Common.Auditing;
using JPCenter.Infrastructure.Common.Idempotency;
using JPCenter.Infrastructure.Common.Time;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JPCenter.Infrastructure.Common.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Dịch vụ hạ tầng chung: đồng hồ, interceptor, nhật ký (hàng đợi + ghi nền), Idempotency.
    /// Các mặc định (người dùng = hệ thống, ghi audit ra log, Idempotency trong bộ nhớ) được Api.Common / module System thay thế.
    /// </summary>
    public static IServiceCollection AddInfrastructureCommon(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.TryAddScoped<IRequestInfo, SystemRequestInfo>();

        services.TryAddScoped<AuditFieldsInterceptor>();
        services.TryAddScoped<DataChangeAuditInterceptor>();
        services.TryAddScoped<IAuditChangeCollector, AuditChangeCollector>();
        services.TryAddScoped<IAuditTrail, AuditTrail>();
        services.TryAddScoped<IAuditSink, LoggingAuditSink>();
        services.TryAddSingleton<ChannelAuditWriter>();
        services.TryAddSingleton<IAuditWriter>(sp => sp.GetRequiredService<ChannelAuditWriter>());
        services.AddHostedService<AuditBackgroundWriter>();

        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        // Email dùng chung mọi module: nội dung (EmailContent) + khuôn thương hiệu + SMTP
        services.TryAddSingleton(sp => EmailOptions.Load(sp.GetRequiredService<IConfiguration>()));
        services.TryAddSingleton<EmailRenderer>();
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

    /// <summary>
    /// Đăng ký DbContext của 1 module: PostgreSQL (chuỗi kết nối <c>ConnectionStrings:Database</c>), snake_case,
    /// bảng lịch sử migration nằm trong schema của module, interceptor chung (cột chung + ghi thay đổi cho nhật ký).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : ModuleDbContextBase
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Database (xem .env.example)");

        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention()
            // Thứ tự quan trọng: đổi xóa → xóa mềm TRƯỚC, rồi mới ghi nhận thay đổi
            .AddInterceptors(sp.GetRequiredService<AuditFieldsInterceptor>(), sp.GetRequiredService<DataChangeAuditInterceptor>()));

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        return services;
    }

    /// <summary>Ngoài HTTP request: không có người dùng — created_by / updated_by để trống (= hệ thống).</summary>
    private sealed class SystemCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? AccountId => null;
        public Guid? SessionId => null;
        public UserAccess? Access => null;
    }

    private sealed class SystemRequestInfo : IRequestInfo
    {
        public string Ip => "system";
        public string Device => "system";
        public string? TraceId => null;
    }
}
