using JPCenter.Api.Common.Paging;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.LeadRules.DeleteLeadRule;
using JPCenter.Modules.Crm.Application.LeadRules.GetAssignmentLogs;
using JPCenter.Modules.Crm.Application.LeadRules.GetLeadRules;
using JPCenter.Modules.Crm.Application.LeadRules.ReorderLeadRules;
using JPCenter.Modules.Crm.Application.LeadRules.SaveLeadRule;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>CRM-09 — quy tắc chia lead & SLA + nhật ký chia lead. Quyền trang <c>crm.lead-rules</c>.</summary>
public static class LeadRuleEndpoints
{
    private const string Page = CrmAccess.LeadRules;

    public sealed record RuleBody(string? Name, bool Active, List<LeadSource>? Sources, List<Guid>? BranchIds, List<string>? Interests,
        RouteMethod Method, List<Guid>? SaleIds, bool OnlyOnShift, int SlaMinutes, List<Guid>? EscalateToIds, bool ReassignOnBreach, uint Version = 0)
    {
        public LeadRuleSettings ToSettings() => new(Name ?? "", Sources ?? [], (BranchIds ?? []).Select(b => new BranchId(b)).ToList(), Interests ?? [],
            Method, SaleIds ?? [], OnlyOnShift, SlaMinutes, EscalateToIds ?? [], ReassignOnBreach);
    }

    public sealed record OrderBody(List<Guid> Ids);

    public static void Map(IEndpointRouteBuilder crm)
    {
        var g = crm.MapGroup("/lead-rules");

        g.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetLeadRulesQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPost("/", async (RuleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveLeadRuleCommand(null, b.ToSettings(), b.Active, 0), ct)).ToCreatedResult(r => $"/api/v1/crm/lead-rules/{r.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPut("/order", async (OrderBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReorderLeadRulesCommand(b.Ids ?? []), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPut("/{id:guid}", async (Guid id, RuleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveLeadRuleCommand(id, b.ToSettings(), b.Active, b.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new DeleteLeadRuleCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Delete);

        crm.MapGet("/lead-assignment-logs", async (Guid? leadId, [AsParameters] PagingRequest paging, ISender s, CancellationToken ct) =>
                (await s.Send(new GetAssignmentLogsQuery(leadId, paging.ToPageRequest()), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);
    }
}
