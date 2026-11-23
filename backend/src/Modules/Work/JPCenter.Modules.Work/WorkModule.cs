using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Work.Api;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using JPCenter.Modules.Work.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Work;

/// <summary>Điểm vào module Work (Công việc) — schema <c>work</c>, API <c>/api/v1/work</c>: task, xếp ca, bàn giao ca (user story 14).</summary>
public sealed class WorkModule : IModule
{
    public string Name => WorkDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<WorkDbContext>(configuration, WorkDbContext.SchemaName);
        services.AddScoped<IWorkDbContext>(sp => sp.GetRequiredService<WorkDbContext>());
        services.AddHandlersFromAssembly(typeof(WorkModule).Assembly);

        services.AddScoped<IDataSeeder, WorkSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => WorkEndpoints.Map(group);
}
