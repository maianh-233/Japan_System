using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Paging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.HrRequests.CancelMyHrRequest;
using JPCenter.Modules.Hr.Application.HrRequests.CreateHrRequest;
using JPCenter.Modules.Hr.Application.HrRequests.DecideHrRequest;
using JPCenter.Modules.Hr.Application.HrRequests.GetHrRequests;
using JPCenter.Modules.Hr.Domain.HrRequests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Hr.Api.Endpoints;

/// <summary>
/// DN-01 → DN-04. Đơn của tôi ở <c>/hr/me/requests</c> (cổng nhân viên). Danh sách <c>/hr/requests?tab=todo|mine|all</c>: tab "all" cần
/// <c>hr.requests.view</c> (kiểm trong handler); duyệt / từ chối: người tới lượt hoặc <c>hr.requests.approve</c> (kiểm trong handler).
/// </summary>
public static class HrRequestEndpoints
{
    public sealed record RequestBody(HrRequestType Type, DateOnly? From, DateOnly? To, bool? PaidLeave, decimal? Amount, string? Title,
        SwapBody? Swap, string Reason, Guid? EmployeeId);

    public sealed record SwapBody(Guid MyShiftId, Guid WithEmployeeId, Guid TheirShiftId);

    public sealed record DecideBody(string? Comment);

    public sealed record RejectBody(string Reason);

    public static void Map(IEndpointRouteBuilder hr)
    {
        var me = hr.MapGroup("/me/requests").RequirePortal(Portal.Staff);
        me.MapGet("/", async (QueryEnum<HrRequestType>? type, QueryEnum<HrRequestStatus>? status, int? page, int? pageSize, ISender s,
                CancellationToken ct) =>
            (await s.Send(new GetHrRequestsQuery(HrRequestTab.Mine, type?.Value, status?.Value, null, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .ToHttpResult());
        me.MapPost("/", async (RequestBody b, ISender s, CancellationToken ct) =>
            (await s.Send(Create(b, null), ct)).ToCreatedResult(r => $"/api/v1/hr/requests/{r.Id}"));
        me.MapPost("/{id:guid}/cancel", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new CancelMyHrRequestCommand(id), ct)).ToHttpResult());

        var g = hr.MapGroup("/requests");
        g.MapGet("/", async (QueryEnum<HrRequestTab>? tab, QueryEnum<HrRequestType>? type, QueryEnum<HrRequestStatus>? status, Guid? branchId,
                    int? page, int? pageSize, ISender s, CancellationToken ct) =>
                (await s.Send(new GetHrRequestsQuery(tab?.Value ?? HrRequestTab.Todo, type?.Value, status?.Value, branchId,
                    new PageRequest(page ?? 1, pageSize ?? 50)), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);
        g.MapPost("/", async (RequestBody b, ISender s, CancellationToken ct) =>
                (await s.Send(Create(b, b.EmployeeId), ct)).ToCreatedResult(r => $"/api/v1/hr/requests/{r.Id}"))
            .RequirePermission(HrScope.Requests, PermissionAction.Create);
        g.MapPost("/{id:guid}/approve", async (Guid id, DecideBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new DecideHrRequestCommand(id, true, b?.Comment), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);
        g.MapPost("/{id:guid}/reject", async (Guid id, RejectBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new DecideHrRequestCommand(id, false, b.Reason), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);
    }

    private static CreateHrRequestCommand Create(RequestBody b, Guid? employeeId) => new(employeeId, b.Type, b.From, b.To, b.PaidLeave, b.Amount,
        b.Title, b.Swap?.MyShiftId, b.Swap?.WithEmployeeId, b.Swap?.TheirShiftId, b.Reason);
}
