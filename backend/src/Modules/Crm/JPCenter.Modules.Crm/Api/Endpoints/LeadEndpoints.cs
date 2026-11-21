using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Idempotency;
using JPCenter.Api.Common.Paging;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.AddConsultation;
using JPCenter.Modules.Crm.Application.Leads.AssignLead;
using JPCenter.Modules.Crm.Application.Leads.ChangeLeadStatus;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Application.Leads.CreateLead;
using JPCenter.Modules.Crm.Application.Leads.EnrollLead;
using JPCenter.Modules.Crm.Application.Leads.FindDuplicates;
using JPCenter.Modules.Crm.Application.Leads.GetLead;
using JPCenter.Modules.Crm.Application.Leads.GetLeadBoard;
using JPCenter.Modules.Crm.Application.Leads.GetLeads;
using JPCenter.Modules.Crm.Application.Leads.ImportLeads;
using JPCenter.Modules.Crm.Application.Leads.LinkStudent;
using JPCenter.Modules.Crm.Application.Leads.SetLeadHidden;
using JPCenter.Modules.Crm.Application.Leads.UnlinkStudent;
using JPCenter.Modules.Crm.Application.Leads.UpdateLead;
using JPCenter.Modules.Crm.Application.Leads.UpdateStudentLink;
using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>CRM-01 → CRM-08 — Lead (<c>/api/v1/crm/leads</c>). Quyền trang <c>crm.leads</c>, phạm vi own = lead mình phụ trách.</summary>
public static class LeadEndpoints
{
    private const string Page = CrmAccess.Leads;
    public const long ImportLimit = 5 * 1024 * 1024;

    public sealed record LeadBody(string? FullName, string? Phone, string? Email, LeadSource Source, Guid BranchId, string? Interest, string? Note,
        Guid? CampaignId, Guid? AssigneeId, DateTimeOffset? NextFollowUpAt, List<JPCenter.SharedKernel.Primitives.ContactChannel>? NotifyChannels,
        LeadStatus Status = LeadStatus.New, string? LostReason = null, uint Version = 0)
    {
        public LeadInput ToInput() => new(FullName, Phone, Email, Source, BranchId, Interest, Note, CampaignId, AssigneeId, NextFollowUpAt,
            NotifyChannels);
    }

    public sealed record StatusBody(LeadStatus Status, string? LostReason);

    public sealed record AssignBody(Guid SaleId, string? Note);

    public sealed record ConsultationBody(ConsultChannel Channel, ConsultResult Result, string? Content, DateTimeOffset? NextFollowUpAt);

    public sealed record LinkBody(Guid StudentId, Relationship Relationship, bool Primary, bool Billing, bool Notify);

    public sealed record LinkFlagsBody(Relationship Relationship, bool Primary, bool Billing, bool Notify);

    public sealed record ImportBody(List<ImportLeadRow> Rows);

    public sealed record EnrollBody(EnrollWho Who, Guid? StudentId, EnrollPerson? Student, Guid? ClassId, decimal? Amount, decimal Discount,
        DateOnly? DueDate);

    /// <summary><c>?status=&amp;source=&amp;branchId=&amp;assigneeId=&amp;unassigned=&amp;campaignId=&amp;slaOverdue=&amp;followUpOverdue=&amp;hidden=&amp;q=</c></summary>
    public sealed record FilterQuery(QueryEnum<LeadStatus>? Status, QueryEnum<LeadSource>? Source, Guid? BranchId, Guid? AssigneeId,
        bool? Unassigned, Guid? CampaignId, bool? SlaOverdue, bool? FollowUpOverdue, bool? Hidden, string? Q)
    {
        public LeadFilter ToFilter() => new(Status?.Value, Source?.Value, BranchId, AssigneeId, Unassigned ?? false, CampaignId, SlaOverdue ?? false,
            FollowUpOverdue ?? false, Hidden, Q);
    }

    public static void Map(IEndpointRouteBuilder crm)
    {
        var g = crm.MapGroup("/leads");

        g.MapGet("/", async ([AsParameters] FilterQuery f, [AsParameters] PagingRequest paging, ISender s, CancellationToken ct) =>
                (await s.Send(new GetLeadsQuery(f.ToFilter(), paging.ToPageRequest()), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapGet("/board", async ([AsParameters] FilterQuery f, ISender s, CancellationToken ct) =>
                (await s.Send(new GetLeadBoardQuery(f.ToFilter()), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapGet("/duplicates", async (string? phone, Guid? exceptId, ISender s, CancellationToken ct) =>
                (await s.Send(new FindDuplicatesQuery(phone ?? "", exceptId), ct)).ToHttpResult())
            .RequireAnyPermission((Page, PermissionAction.Create), (Page, PermissionAction.Update));

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetLeadQuery(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPost("/", async (LeadBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateLeadCommand(b.ToInput(), b.Status, b.LostReason), ct)).ToCreatedResult(x => $"/api/v1/crm/leads/{x.Lead.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPost("/import", async (ImportBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ImportLeadsCommand(b.Rows ?? []), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Create)
            .RequireRateLimiting(RateLimitPolicies.Import)
            .WithBodyLimit(ImportLimit);

        g.MapPut("/{id:guid}", async (Guid id, LeadBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateLeadCommand(id, b.ToInput(), b.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/status", async (Guid id, StatusBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeLeadStatusCommand(id, b.Status, b.LostReason), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/assign", async (Guid id, AssignBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AssignLeadCommand(id, b.SaleId, b.Note), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/consultations", async (Guid id, ConsultationBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddConsultationCommand(id, b.Channel, b.Result, b.Content, b.NextFollowUpAt), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/hide", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new SetLeadHiddenCommand(id, true), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/unhide", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new SetLeadHiddenCommand(id, false), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        // CRM-08 — học viên liên kết
        g.MapPost("/{id:guid}/students", async (Guid id, LinkBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new LinkStudentCommand(id, b.StudentId, b.Relationship, b.Primary, b.Billing, b.Notify), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPut("/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, LinkFlagsBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateStudentLinkCommand(id, studentId, b.Relationship, b.Primary, b.Billing, b.Notify), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new UnlinkStudentCommand(id, studentId), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        // CRM-07 — chốt lead → HV + ghi danh (+ hóa đơn). Quyền tạo hồ sơ HV kiểm trong handler (chỉ khi tạo người học mới)
        g.MapPost("/{id:guid}/enroll", async (Guid id, EnrollBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new EnrollLeadCommand(id, b.Who, b.StudentId, b.Student, b.ClassId, b.Amount, b.Discount, b.DueDate), ct))
                .ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update)
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive)
            .Idempotent();
    }
}
