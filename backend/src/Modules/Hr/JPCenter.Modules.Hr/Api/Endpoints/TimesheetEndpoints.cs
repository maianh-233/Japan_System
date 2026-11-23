using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Timesheets.CheckIn;
using JPCenter.Modules.Hr.Application.Timesheets.CheckOut;
using JPCenter.Modules.Hr.Application.Timesheets.EditTimeRecord;
using JPCenter.Modules.Hr.Application.Timesheets.GetMyTimesheet;
using JPCenter.Modules.Hr.Application.Timesheets.GetTimesheets;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Hr.Api.Endpoints;

/// <summary>
/// CC-01 → CC-04. Trang cá nhân <c>/hr/me/...</c> (cổng nhân viên, không cần mã quyền — chỉ dữ liệu của mình) ·
/// quản lý <c>/hr/timesheets</c> (<c>hr.timesheets.*</c>).
/// </summary>
public static class TimesheetEndpoints
{
    public sealed record EditBody(TimeOnly? CheckIn, TimeOnly? CheckOut, string Reason);

    private static readonly Error InvalidDate = Error.Validation("hr.time_record.invalid_date", "Ngày không hợp lệ (YYYY-MM-DD)");

    public static void Map(IEndpointRouteBuilder hr)
    {
        var me = hr.MapGroup("/me").RequirePortal(Portal.Staff);
        me.MapPost("/check-in", async (ISender s, CancellationToken ct) => (await s.Send(new CheckInCommand(), ct)).ToHttpResult());
        me.MapPost("/check-out", async (ISender s, CancellationToken ct) => (await s.Send(new CheckOutCommand(), ct)).ToHttpResult());
        me.MapGet("/timesheet", async (string? month, ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyTimesheetQuery(month), ct)).ToHttpResult());

        var g = hr.MapGroup("/timesheets");
        g.MapGet("/", async (string? month, Guid? branchId, QueryEnum<Department>? department, bool? problemsOnly, ISender s, CancellationToken ct) =>
                (await s.Send(new GetTimesheetsQuery(month, branchId, department?.Value, problemsOnly ?? false), ct)).ToHttpResult())
            .RequirePermission(HrScope.Timesheets, PermissionAction.View);
        // {date} đọc dạng chuỗi để kiểm quyền trước khi kiểm định dạng ngày (YYYY-MM-DD)
        g.MapPut("/{employeeId:guid}/{date}", async (Guid employeeId, string date, EditBody b, ISender s, CancellationToken ct) =>
                DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)
                    ? (await s.Send(new EditTimeRecordCommand(employeeId, day, b.CheckIn, b.CheckOut, b.Reason), ct)).ToHttpResult()
                    : InvalidDate.ToProblem())
            .RequirePermission(HrScope.Timesheets, PermissionAction.Update);
    }
}
