using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Application.Shifts.FillWeek;
using JPCenter.Modules.Work.Application.Shifts.GetShiftWeek;
using JPCenter.Modules.Work.Application.Shifts.MyShifts;
using JPCenter.Modules.Work.Application.Shifts.ToggleShift;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Work.Api.Endpoints;

/// <summary>XC-01 → XC-04 — lưới xếp ca <c>/work/shifts</c> (<c>work.shifts.view / update</c>, phạm vi chi nhánh); lịch rảnh / bận của tôi <c>/work/me/...</c>.</summary>
public static class ShiftEndpoints
{
    public sealed record ToggleBody(Guid EmployeeId, DateOnly Date, ShiftKey Shift, bool? Assign);

    public sealed record WeekBody(Guid BranchId, DateOnly? Week);

    public sealed record RegistrationBody(DateOnly Date, ShiftKey Shift, ShiftRegistrationKind? Status);

    public static void Map(IEndpointRouteBuilder work)
    {
        var g = work.MapGroup("/shifts");
        g.MapGet("/", async (Guid? branchId, DateOnly? week, ISender s, CancellationToken ct) =>
                (await s.Send(new GetShiftWeekQuery(branchId, week), ct)).ToHttpResult())
            .RequirePermission(ShiftDesk.Page, PermissionAction.View);
        g.MapPost("/toggle", async (ToggleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ToggleShiftCommand(b.EmployeeId, b.Date, b.Shift, b.Assign), ct)).ToHttpResult())
            .RequirePermission(ShiftDesk.Page, PermissionAction.Update);
        g.MapPost("/apply-registrations", async (WeekBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new FillWeekCommand(b.BranchId, b.Week, FillMode.FromRegistrations), ct)).ToHttpResult())
            .RequirePermission(ShiftDesk.Page, PermissionAction.Update);
        g.MapPost("/copy-week", async (WeekBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new FillWeekCommand(b.BranchId, b.Week, FillMode.CopyPreviousWeek), ct)).ToHttpResult())
            .RequirePermission(ShiftDesk.Page, PermissionAction.Update);

        var me = work.MapGroup("/me/shift-registrations").RequirePortal(Portal.Staff);
        me.MapGet("/", async (DateOnly? from, ISender s, CancellationToken ct) => (await s.Send(new GetMyShiftsQuery(from), ct)).ToHttpResult());
        me.MapPut("/", async (RegistrationBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SetMyShiftRegistrationCommand(b.Date, b.Shift, b.Status), ct)).ToHttpResult());
    }
}
