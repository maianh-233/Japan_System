using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.Hr.Infrastructure.Persistence.Seed;
using JPCenter.Modules.Hr.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Hr;

/// <summary>Điểm vào module Hr (Nhân sự) — schema <c>hr</c>, API <c>/api/v1/hr</c>.</summary>
public sealed class HrModule : IModule
{
    public string Name => HrDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<HrDbContext>(configuration, HrDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(HrModule).Assembly);
        services.AddScoped<IEmployeeDirectory, EmployeeDirectory>();
        services.AddScoped<IDataSeeder, HrSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        // Endpoint Nhân sự làm khi tới chức năng Nhân sự
    }
}
