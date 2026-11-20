using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using JPCenter.Modules.Crm.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Crm.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Crm;

/// <summary>
/// Điểm vào module Crm (Tuyển sinh) — schema <c>crm</c>, API <c>/api/v1/crm</c>.
/// Giai đoạn 15: lưu lead + liên kết học viên để cổng PH thấy con (<see cref="IGuardianDirectory"/>); màn hình Tuyển sinh làm ở giai đoạn 16.
/// </summary>
public sealed class CrmModule : IModule
{
    public string Name => CrmDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.SchemaName);
        services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        services.AddHandlersFromAssembly(typeof(CrmModule).Assembly);

        // Cho module khác (Crm.Contracts)
        services.AddScoped<IGuardianDirectory, GuardianDirectory>();

        services.AddScoped<IDataSeeder, CrmSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) { }
}
