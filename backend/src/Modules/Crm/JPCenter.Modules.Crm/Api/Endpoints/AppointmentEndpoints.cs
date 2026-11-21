using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Appointments.CheckAppointment;
using JPCenter.Modules.Crm.Application.Appointments.CloseAppointment;
using JPCenter.Modules.Crm.Application.Appointments.CreateAppointment;
using JPCenter.Modules.Crm.Application.Appointments.GetAppointments;
using JPCenter.Modules.Crm.Application.Appointments.RescheduleAppointment;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>CRM-14, CRM-15 — lịch hẹn tư vấn / test tại trung tâm. Quyền trang <c>crm.appointments</c> (phạm vi chi nhánh).</summary>
public static class AppointmentEndpoints
{
    private const string Page = CrmAccess.Appointments;

    public sealed record AppointmentBody(Guid LeadId, AppointmentKind Kind, Guid? BranchId, Guid? RoomId, Guid StaffId, DateTimeOffset Start,
        DateTimeOffset? End, string? Note);

    public sealed record CheckBody(Guid? Id, Guid LeadId, AppointmentKind Kind, Guid? RoomId, Guid StaffId, DateTimeOffset Start, DateTimeOffset? End);

    public sealed record RescheduleBody(Guid? RoomId, Guid StaffId, DateTimeOffset Start, DateTimeOffset End, string? Note, uint Version = 0);

    public sealed record TextBody(string? Text);

    public static void Map(IEndpointRouteBuilder crm)
    {
        var g = crm.MapGroup("/appointments");

        g.MapGet("/", async (DateTimeOffset? from, DateTimeOffset? to, Guid? branchId, Guid? leadId, Guid? staffId, QueryEnum<AppointmentStatus>? status,
                    ISender s, CancellationToken ct) =>
                (await s.Send(new GetAppointmentsQuery(from, to, branchId, leadId, staffId, status?.Value), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPost("/check", async (CheckBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CheckAppointmentQuery(b.Id, b.LeadId, b.RoomId, b.StaffId, b.Start,
                    b.End ?? b.Start.AddMinutes(Appointment.DefaultDuration(b.Kind))), ct)).ToHttpResult())
            .RequireAnyPermission((Page, PermissionAction.Create), (Page, PermissionAction.Update));

        g.MapPost("/", async (AppointmentBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateAppointmentCommand(b.LeadId, b.Kind, b.BranchId, b.RoomId, b.StaffId, b.Start, b.End, b.Note), ct))
                .ToCreatedResult(a => $"/api/v1/crm/appointments/{a.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, RescheduleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RescheduleAppointmentCommand(id, b.RoomId, b.StaffId, b.Start, b.End, b.Note, b.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        MapClose(g, "done", AppointmentOutcome.Done);
        MapClose(g, "no-show", AppointmentOutcome.NoShow);
        MapClose(g, "cancel", AppointmentOutcome.Cancel);
    }

    private static void MapClose(RouteGroupBuilder g, string path, AppointmentOutcome outcome) =>
        g.MapPost($"/{{id:guid}}/{path}", async (Guid id, TextBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new CloseAppointmentCommand(id, outcome, b?.Text), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
}
