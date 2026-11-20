using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Trash.Common;
using JPCenter.Modules.System.Application.Trash.GetTrash;
using JPCenter.Modules.System.Application.Trash.PurgeTrashItem;
using JPCenter.Modules.System.Application.Trash.RestoreTrashItem;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>TR-01 → TR-03 — Thùng rác. Quyền: Hệ thống › Thùng rác (view · update = khôi phục · delete = xóa vĩnh viễn).</summary>
public static class TrashEndpoints
{
    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/trash");

        group.MapGet("/", async (Guid? branchId, string? kind, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetTrashQuery(branchId, kind), ct)).ToHttpResult())
            .RequirePermission(TrashBin.Page, PermissionAction.View);

        group.MapPost("/{id:guid}/restore", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RestoreTrashItemCommand(id), ct)).ToHttpResult())
            .RequirePermission(TrashBin.Page, PermissionAction.Update);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PurgeTrashItemCommand(id), ct)).ToHttpResult())
            .RequirePermission(TrashBin.Page, PermissionAction.Delete);
    }
}
