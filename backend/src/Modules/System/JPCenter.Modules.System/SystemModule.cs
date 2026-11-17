using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Idempotency;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.Modules.System.Infrastructure.Persistence.Seed;
using JPCenter.Modules.System.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JPCenter.Modules.System;

/// <summary>
/// Điểm vào module System — schema <c>system</c>, API <c>/api/v1/system</c>.
/// Cung cấp cho middleware chung: nạp quyền (<see cref="IUserAccessProvider"/>), lưu nhật ký (<see cref="IAuditSink"/>),
/// lưu Idempotency-Key trong PostgreSQL (<see cref="IIdempotencyStore"/>) — thay các bản mặc định trong bộ nhớ.
/// </summary>
public sealed class SystemModule : IModule
{
    public string Name => SystemDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SystemDbContext>(configuration, SystemDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(SystemModule).Assembly);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.Replace(ServiceDescriptor.Scoped<IUserAccessProvider, UserAccessProvider>());
        services.Replace(ServiceDescriptor.Scoped<IAuditSink, AuditLogSink>());
        services.Replace(ServiceDescriptor.Scoped<IIdempotencyStore, PostgresIdempotencyStore>());
        services.AddScoped<IDataSeeder, SystemSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        // Endpoint đăng nhập / phân quyền: giai đoạn 4, 5
    }
}
