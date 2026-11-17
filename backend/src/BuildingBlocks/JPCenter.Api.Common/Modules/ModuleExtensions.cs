using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Api.Common.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration, params IModule[] modules)
    {
        foreach (var module in modules)
        {
            module.AddServices(services, configuration);
            services.AddSingleton(module);
        }
        return services;
    }

    /// <summary>Mỗi module có group <c>/api/v1/&lt;module&gt;</c> (CLAUDE.md mục 8).</summary>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder app)
    {
        foreach (var module in app.ServiceProvider.GetServices<IModule>())
            module.MapEndpoints(app.MapGroup($"/api/v1/{module.Name}").WithTags(module.Name));
        return app;
    }
}
