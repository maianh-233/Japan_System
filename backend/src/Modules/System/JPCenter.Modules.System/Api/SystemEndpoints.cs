using JPCenter.Modules.System.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api;

/// <summary>Mọi endpoint module System trên group <c>/api/v1/system</c>.</summary>
public static class SystemEndpoints
{
    public static void Map(IEndpointRouteBuilder system)
    {
        AuthEndpoints.Map(system);
        MySessionEndpoints.Map(system);
        PortalAccountEndpoints.Map(system);
        RoleEndpoints.Map(system);
        UserPermissionEndpoints.Map(system);
        ApprovalEndpoints.Map(system);
    }
}
