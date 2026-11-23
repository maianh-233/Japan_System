using JPCenter.Modules.Work.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Work.Api;

/// <summary>Mọi endpoint module Work trên group <c>/api/v1/work</c> (user story 14).</summary>
public static class WorkEndpoints
{
    public static void Map(IEndpointRouteBuilder work)
    {
        TaskEndpoints.Map(work);
        ShiftEndpoints.Map(work);
        HandoverEndpoints.Map(work);
    }
}
