using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Reporting.Api;
using JPCenter.Modules.Reporting.Application.Common;
using JPCenter.Modules.Reporting.Application.Reports.Common;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Modules.Reporting;

/// <summary>
/// Điểm vào module Reporting (Báo cáo + bảng điều khiển) — API <c>/api/v1/reporting</c>, chỉ đọc.
/// Không có DbContext / bảng: đọc số liệu qua Contracts của module sở hữu dữ liệu (ADR 0004, user story 17).
/// </summary>
public sealed class ReportingModule : IModule
{
    public const string SchemaName = "reporting";

    public string Name => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(ReportingModule).Assembly);
        services.AddScoped<ReportingAccess>();
        services.AddScoped<ReportBuilder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder group) => ReportingEndpoints.Map(group);
}
