using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Messages.DeleteMessageTemplate;
using JPCenter.Modules.Crm.Application.Messages.GetMessages;
using JPCenter.Modules.Crm.Application.Messages.GetMessageTemplates;
using JPCenter.Modules.Crm.Application.Messages.SaveMessageTemplate;
using JPCenter.Modules.Crm.Application.Messages.SendMessage;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>
/// CRM-17 — mẫu tin (quyền <c>crm.message-templates</c>) + gửi / xem tin: theo quyền trang đang đứng (lead, hồ sơ HV, lớp, hóa đơn).
/// </summary>
public static class MessageEndpoints
{
    private const string Page = CrmAccess.MessageTemplates;

    private static readonly (string, PermissionAction)[] SendFrom =
    [
        (CrmAccess.Leads, PermissionAction.Update), ("students.profiles", PermissionAction.Update), ("students.contacts", PermissionAction.Update),
        ("academic.classes", PermissionAction.Update), ("finance.invoices", PermissionAction.Update),
    ];

    private static readonly (string, PermissionAction)[] ViewFrom =
    [
        (CrmAccess.Leads, PermissionAction.View), ("students.profiles", PermissionAction.View), ("students.contacts", PermissionAction.View),
    ];

    public sealed record TemplateBody(MessageScenario Scenario, string? Name, ContactChannel Channel, string? Body, bool Active = true, uint Version = 0);

    public sealed record SendBody(ContactChannel Channel, MessageScenario Scenario, Guid? TemplateId, Guid? LeadId, Guid? StudentId, string? ToName,
        string? ToPhone, string? Body);

    public static void Map(IEndpointRouteBuilder crm)
    {
        var t = crm.MapGroup("/message-templates");

        t.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetMessageTemplatesQuery(), ct)).ToHttpResult())
            .RequireAnyPermission([(Page, PermissionAction.View), .. SendFrom]);

        t.MapPost("/", async (TemplateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveMessageTemplateCommand(null, b.Scenario, b.Name, b.Channel, b.Body, b.Active, 0), ct))
                .ToCreatedResult(x => $"/api/v1/crm/message-templates/{x.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        t.MapPut("/{id:guid}", async (Guid id, TemplateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveMessageTemplateCommand(id, b.Scenario, b.Name, b.Channel, b.Body, b.Active, b.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        t.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new DeleteMessageTemplateCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Delete);

        crm.MapGet("/messages", async (Guid? leadId, Guid? studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetMessagesQuery(leadId, studentId), ct)).ToHttpResult())
            .RequireAnyPermission(ViewFrom);

        crm.MapPost("/messages", async (SendBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SendMessageCommand(b.Channel, b.Scenario, b.TemplateId, b.LeadId, b.StudentId, b.ToName, b.ToPhone, b.Body), ct))
                .ToHttpResult())
            .RequireAnyPermission(SendFrom)
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive);
    }
}
