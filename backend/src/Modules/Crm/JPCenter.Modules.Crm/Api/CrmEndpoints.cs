using JPCenter.Modules.Crm.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api;

/// <summary>Mọi endpoint module Crm trên group <c>/api/v1/crm</c>.</summary>
public static class CrmEndpoints
{
    public static void Map(IEndpointRouteBuilder crm)
    {
        LeadEndpoints.Map(crm);
        LeadRuleEndpoints.Map(crm);
        PlacementEndpoints.Map(crm);
        AppointmentEndpoints.Map(crm);
        CampaignEndpoints.Map(crm);
        MessageEndpoints.Map(crm);
        FunnelEndpoints.Map(crm);
    }
}
