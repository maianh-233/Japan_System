using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Guardians.Common;
using JPCenter.Modules.Crm.Application.Guardians.CreateGuardian;
using JPCenter.Modules.Crm.Application.Guardians.GetGuardians;
using JPCenter.Modules.Crm.Application.Guardians.UpdateGuardian;
using JPCenter.Modules.Crm.Application.Leads.LinkStudent;
using JPCenter.Modules.Crm.Application.Leads.UnlinkStudent;
using JPCenter.Modules.Crm.Application.Leads.UpdateStudentLink;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>
/// HV-12 — Phụ huynh / người giám hộ (<c>/api/v1/crm/guardians</c>). Dữ liệu là lead của Crm nhưng trang thuộc Học viên:
/// quyền <c>students.contacts</c> (giáo vụ không cần quyền Tuyển sinh). Liên kết HV dùng chung luật CRM-08.
/// </summary>
public static class GuardianEndpoints
{
    private const string Page = GuardianAccess.Page;

    public sealed record UpdateBody(string? FullName, string? Phone, string? Email, Guid BranchId, List<ContactChannel>? NotifyChannels, string? Note,
        uint Version = 0);

    public sealed record LinkBody(Guid StudentId, Relationship Relationship, bool Primary, bool Billing, bool Notify);

    public sealed record LinkFlagsBody(Relationship Relationship, bool Primary, bool Billing, bool Notify);

    public static void Map(IEndpointRouteBuilder crm)
    {
        var g = crm.MapGroup("/guardians");

        g.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetGuardiansQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPost("/", async (GuardianInput b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateGuardianCommand(b), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, UpdateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateGuardianCommand(id, b.FullName, b.Phone, b.Email, b.BranchId, b.NotifyChannels, b.Note, b.Version), ct))
                .ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/students", async (Guid id, LinkBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new LinkStudentCommand(id, b.StudentId, b.Relationship, b.Primary, b.Billing, b.Notify, ViaContacts: true), ct))
                .ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPut("/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, LinkFlagsBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateStudentLinkCommand(id, studentId, b.Relationship, b.Primary, b.Billing, b.Notify, ViaContacts: true), ct))
                .ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new UnlinkStudentCommand(id, studentId, ViaContacts: true), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
