using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Branches.CloseBranch;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Application.Branches.CreateBranch;
using JPCenter.Modules.System.Application.Branches.GetBranches;
using JPCenter.Modules.System.Application.Branches.GetBranchOptions;
using JPCenter.Modules.System.Application.Branches.OpenBranch;
using JPCenter.Modules.System.Application.Branches.ReopenBranch;
using JPCenter.Modules.System.Application.Branches.UpdateBranch;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>CN-01 → CN-08 — chi nhánh. Quyền: Hệ thống › Chi nhánh (ô chọn chi nhánh: mọi người đã đăng nhập).</summary>
public static class BranchEndpoints
{
    private const string Page = "system.branches";

    public sealed record UpdateBody(string Name, string Address, string Phone, string Email, Guid? ManagerId, string OpenHours, DateOnly OpenedAt,
        string? InvoicePrefix, BankInput? Bank, string? Note, uint Version);

    public sealed record CloseBody(string ConfirmCode);

    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/branches");

        group.MapGet("/", async (string? status, string? q, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetBranchesQuery(status, q), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapGet("/options", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetBranchOptionsQuery(), ct)).ToHttpResult())
            .RequireSignedIn();

        group.MapPost("/", async (CreateBranchCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToCreatedResult(b => $"/api/v1/system/branches/{b.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        group.MapPut("/{id:guid}", async (Guid id, UpdateBody b, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateBranchCommand(id, b.Name, b.Address, b.Phone, b.Email, b.ManagerId, b.OpenHours, b.OpenedAt,
                    b.InvoicePrefix, b.Bank, b.Note, b.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/open", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new OpenBranchCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/close", async (Guid id, CloseBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CloseBranchCommand(id, body.ConfirmCode), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/reopen", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ReopenBranchCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
