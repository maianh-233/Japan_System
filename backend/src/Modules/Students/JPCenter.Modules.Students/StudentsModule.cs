using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Students.Api;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using JPCenter.Modules.Students.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Students;

/// <summary>Điểm vào module Students (Học viên) — schema <c>students</c>, API <c>/api/v1/students</c>.</summary>
public sealed class StudentsModule : IModule
{
    public string Name => StudentsDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudentsDbContext>(configuration, StudentsDbContext.SchemaName);
        services.AddScoped<IStudentsDbContext>(sp => sp.GetRequiredService<StudentsDbContext>());
        services.AddHandlersFromAssembly(typeof(StudentsModule).Assembly);

        services.AddScoped<IDataSeeder, StudentSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => StudentsEndpoints.Map(group);
}
