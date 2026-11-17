using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Employees.CancelScheduledQuit;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Application.Employees.CreateEmployee;
using JPCenter.Modules.Hr.Application.Employees.GetEmployee;
using JPCenter.Modules.Hr.Application.Employees.GetEmployees;
using JPCenter.Modules.Hr.Application.Employees.MyProfile;
using JPCenter.Modules.Hr.Application.Employees.QuitEmployee;
using JPCenter.Modules.Hr.Application.Employees.RenewContract;
using JPCenter.Modules.Hr.Application.Employees.SetLongLeave;
using JPCenter.Modules.Hr.Application.Employees.UpdateAvailability;
using JPCenter.Modules.Hr.Application.Employees.UpdateEmployee;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Hr.Api.Endpoints;

/// <summary>
/// NS-01 → NS-10. 2 nhóm đường dẫn cùng 1 bảng nhân sự, mỗi nhóm 1 mã quyền (khớp 2 trang frontend):
/// <c>/hr/employees</c> → <c>hr.employees.*</c> (nhân viên) · <c>/hr/teachers</c> → <c>hr.teachers.*</c> (giáo viên, trợ giảng).
/// Hồ sơ khác nhóm / ngoài phạm vi → 404.
/// </summary>
public static class EmployeeEndpoints
{
    public sealed record CreateBody(EmployeeKind? Kind, string FullName, Gender Gender, DateOnly? Dob, string Phone, string Email, Guid BranchId,
        Department Department, string Position, Guid? ManagerId, bool ShiftBased, EmployeeStatus Status, ContractInput Contract,
        TeacherInput? Teacher, string? Note);

    public sealed record UpdateBody(string FullName, Gender Gender, DateOnly? Dob, string Phone, string Email, Guid BranchId, Department Department,
        string Position, Guid? ManagerId, bool ShiftBased, EmployeeStatus Status, TeacherInput? Teacher, string? Note, uint Version);

    public sealed record RenewBody(ContractType? Type, DateOnly? End, decimal? BaseSalary, decimal? Allowance);

    public sealed record QuitBody(string Reason, DateOnly? QuitAt);

    public sealed record AvailabilityBody(Dictionary<int, List<ShiftKey>> Availability);

    public sealed record MyContactBody(string Phone, DateOnly? Dob);

    public static void Map(IEndpointRouteBuilder hr)
    {
        MapGroup(hr.MapGroup("/employees"), EmployeeGroup.Staff);
        var teachers = hr.MapGroup("/teachers");
        MapGroup(teachers, EmployeeGroup.Teaching);

        // NS-08 — lịch rảnh: chỉ giáo viên / trợ giảng
        teachers.MapPut("/{id:guid}/availability", async (Guid id, AvailabilityBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateAvailabilityCommand(id, body.Availability), ct)).ToHttpResult())
            .RequirePermission(EmployeeGroup.Teaching.Page(), PermissionAction.Update);

        // NS-11 — hồ sơ của tôi: không cần mã quyền, chỉ dữ liệu của chính người đăng nhập (cổng nhân viên / GV)
        var me = hr.MapGroup("/me").RequirePortal(Portal.Staff, Portal.Teacher);
        me.MapGet("/", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyProfileQuery(), ct)).ToHttpResult());
        me.MapPut("/contact", async (MyContactBody b, ISender sender, CancellationToken ct) =>
            (await sender.Send(new UpdateMyContactCommand(b.Phone, b.Dob), ct)).ToHttpResult());
    }

    private static void MapGroup(RouteGroupBuilder g, EmployeeGroup scope)
    {
        var page = scope.Page();
        var defaultKind = scope == EmployeeGroup.Staff ? EmployeeKind.Staff : EmployeeKind.Teacher;

        g.MapGet("/", async (Guid? branchId, string? group, string? status, string? level, string? special, bool? includeQuit, string? q,
                    int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetEmployeesQuery(scope, branchId, group, ParseStatus(status), level, special, includeQuit ?? false, q,
                    page ?? 1, pageSize ?? 100), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetEmployeeQuery(scope, id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/", async (CreateBody b, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateEmployeeCommand(scope, b.Kind ?? defaultKind, b.FullName, b.Gender, b.Dob, b.Phone, b.Email, b.BranchId,
                    b.Department, b.Position, b.ManagerId, b.ShiftBased, b.Status, b.Contract, b.Teacher, b.Note), ct))
                .ToCreatedResult(e => $"/api/v1/hr/{(scope == EmployeeGroup.Staff ? "employees" : "teachers")}/{e.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, UpdateBody b, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateEmployeeCommand(scope, id, b.FullName, b.Gender, b.Dob, b.Phone, b.Email, b.BranchId, b.Department,
                    b.Position, b.ManagerId, b.ShiftBased, b.Status, b.Teacher, b.Note, b.Version), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/contracts", async (Guid id, RenewBody b, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RenewContractCommand(scope, id, b.Type, b.End, b.BaseSalary, b.Allowance), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/quit", async (Guid id, QuitBody b, ISender sender, CancellationToken ct) =>
                (await sender.Send(new QuitEmployeeCommand(scope, id, b.Reason, b.QuitAt), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/quit", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CancelScheduledQuitCommand(scope, id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/long-leave", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetLongLeaveCommand(scope, id, true), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/return", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetLongLeaveCommand(scope, id, false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
    }

    private static EmployeeStatus? ParseStatus(string? s) =>
        Enum.TryParse<EmployeeStatus>(s, ignoreCase: true, out var v) ? v : null;
}
