using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Funnel.GetFunnel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>CRM-18 — phễu chuyển đổi <c>?from=YYYY-MM-DD&amp;to=YYYY-MM-DD&amp;branchId=</c>. Quyền trang <c>crm.funnel</c>.</summary>
public static class FunnelEndpoints
{
    public static void Map(IEndpointRouteBuilder crm) =>
        crm.MapGet("/funnel", async (DateOnly? from, DateOnly? to, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetFunnelQuery(from, to, branchId), ct)).ToHttpResult())
            .RequirePermission(CrmAccess.Funnel, PermissionAction.View);
}
