using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.Modules.Finance.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Finance;

/// <summary>
/// Điểm vào module Finance (Tài chính, user story 08) — schema <c>finance</c>, API <c>/api/v1/finance</c>.
/// Giai đoạn 19 phase 2: lưu DB + dữ liệu mẫu; endpoint + job nhắc học phí ở phase 3.
/// </summary>
public sealed class FinanceModule : IModule
{
    public string Name => FinanceDbContext.SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<FinanceDbContext>(configuration, FinanceDbContext.SchemaName);
        services.AddScoped<IFinanceDbContext>(sp => sp.GetRequiredService<FinanceDbContext>());
        services.AddHandlersFromAssembly(typeof(FinanceModule).Assembly);

        services.AddScoped<IDataSeeder, FinanceSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
    }
}
