using JPCenter.Api.Common.Modules;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Infrastructure.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Crm;

/// <summary>
/// Điểm vào module Crm (Tuyển sinh) — schema <c>crm</c>, API <c>/api/v1/crm</c>.
/// ⏳ Giai đoạn 15 mới cung cấp <see cref="IGuardianDirectory"/> cho cổng PH (bản tạm); DbContext + endpoint làm ở giai đoạn 16.
/// </summary>
public sealed class CrmModule : IModule
{
    public string Name => "crm";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Cho module khác (Crm.Contracts)
        services.AddScoped<IGuardianDirectory, PendingGuardianDirectory>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) { }
}
