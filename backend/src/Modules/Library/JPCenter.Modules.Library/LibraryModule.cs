using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Library.Api;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.TeacherDocuments.Common;
using JPCenter.Modules.Library.Infrastructure.Persistence;
using JPCenter.Modules.Library.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Library;

/// <summary>
/// Điểm vào module Library (Thư viện) — schema <c>library</c>, API <c>/api/v1/library</c>.
/// Giai đoạn 20: tài liệu GV gửi lớp (endpoint cổng GV / HV). Thư viện điện tử: giai đoạn 23.
/// </summary>
public sealed class LibraryModule : IModule
{
    public string Name => LibraryDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LibraryDbContext>(configuration, LibraryDbContext.SchemaName);
        services.AddScoped<ILibraryDbContext>(sp => sp.GetRequiredService<LibraryDbContext>());
        services.AddHandlersFromAssembly(typeof(LibraryModule).Assembly);

        services.AddScoped<TeacherDocs>();
        services.AddScoped<IDataSeeder, LibrarySeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => LibraryEndpoints.Map(group);
}
