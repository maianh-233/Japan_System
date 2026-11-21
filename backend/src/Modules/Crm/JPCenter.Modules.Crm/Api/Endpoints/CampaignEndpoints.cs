using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Campaigns.ChangeCampaignStatus;
using JPCenter.Modules.Crm.Application.Campaigns.GetCampaigns;
using JPCenter.Modules.Crm.Application.Campaigns.SaveCampaign;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Campaigns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>CRM-16 — chiến dịch marketing (kèm số liệu). Quyền trang <c>crm.campaigns</c>.</summary>
public static class CampaignEndpoints
{
    private const string Page = CrmAccess.Campaigns;

    public sealed record StatusBody(CampaignStatus Status);

    public static void Map(IEndpointRouteBuilder crm)
    {
        var g = crm.MapGroup("/campaigns");

        g.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetCampaignsQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPost("/", async (CampaignInput b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveCampaignCommand(null, b, 0), ct)).ToCreatedResult(c => $"/api/v1/crm/campaigns/{c.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, CampaignInput b, uint? version, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveCampaignCommand(id, b, version ?? 0), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/status", async (Guid id, StatusBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeCampaignStatusCommand(id, b.Status), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
