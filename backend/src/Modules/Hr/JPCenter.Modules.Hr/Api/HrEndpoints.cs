using JPCenter.Modules.Hr.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Hr.Api;

/// <summary>Mọi endpoint module Hr trên group <c>/api/v1/hr</c>.</summary>
public static class HrEndpoints
{
    public static void Map(IEndpointRouteBuilder hr) => EmployeeEndpoints.Map(hr);
}
