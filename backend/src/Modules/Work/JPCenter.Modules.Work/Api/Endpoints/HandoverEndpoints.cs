using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Handovers.Common;
using JPCenter.Modules.Work.Application.Handovers.CreateHandover;
using JPCenter.Modules.Work.Application.Handovers.GetHandoverDraft;
using JPCenter.Modules.Work.Application.Handovers.GetHandovers;
using JPCenter.Modules.Work.Application.Handovers.ReceiveHandover;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Work.Api.Endpoints;

/// <summary>
/// BG-01 → BG-03 — <c>/work/handovers</c>. Danh sách: cổng nhân viên (theo <c>work.handover.view</c> hoặc bàn giao mình gửi / nhận — lọc trong handler);
/// tạo: <c>work.handover.create</c>; nhận / tick việc tồn: chỉ người nhận (kiểm trong domain).
/// </summary>
public static class HandoverEndpoints
{
    public sealed record ReceiveBody(decimal Counted, string? Note);

    public sealed record DoneBody(bool Done);

    public static void Map(IEndpointRouteBuilder work)
    {
        var g = work.MapGroup("/handovers");
        g.MapGet("/", async (QueryEnum<HandoverStatus>? status, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetHandoversQuery(status?.Value, branchId), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);
        g.MapGet("/draft", async (QueryEnum<ShiftKey>? shift, ISender s, CancellationToken ct) =>
                (await s.Send(new GetHandoverDraftQuery(shift?.Value), ct)).ToHttpResult())
            .RequirePermission(HandoverDesk.Page, PermissionAction.Create);
        g.MapPost("/", async (CreateHandoverCommand b, ISender s, CancellationToken ct) =>
                (await s.Send(b, ct)).ToCreatedResult(h => $"/api/v1/work/handovers/{h.Id}"))
            .RequirePermission(HandoverDesk.Page, PermissionAction.Create);
        g.MapPost("/{id:guid}/receive", async (Guid id, ReceiveBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReceiveHandoverCommand(id, b.Counted, b.Note), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);
        g.MapPut("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, DoneBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new TickHandoverItemCommand(id, itemId, b.Done), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);
    }
}
