using JPCenter.Modules.Academic.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api;

/// <summary>Mọi endpoint module Academic trên group <c>/api/v1/academic</c>.</summary>
public static class AcademicEndpoints
{
    public static void Map(IEndpointRouteBuilder academic)
    {
        CourseEndpoints.Map(academic);
        CurriculumEndpoints.Map(academic);
        RoomEndpoints.Map(academic);
        ClassEndpoints.Map(academic);
        ClassSessionEndpoints.Map(academic);
    }
}
