using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Roles.CreateRole;
using JPCenter.Modules.System.Application.Roles.DeleteRole;
using JPCenter.Modules.System.Application.Roles.GetPermissionCatalog;
using JPCenter.Modules.System.Application.Roles.GetRoles;
using JPCenter.Modules.System.Application.Roles.UpdateRoleGrants;
using JPCenter.Modules.System.Application.Roles.UpdateRoleInfo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>PQ-01 → PQ-04 — vai trò & ma trận quyền. Quyền: Hệ thống › Vai trò & quyền.</summary>
public static class RoleEndpoints
{
    private const string Page = "system.roles";

    public sealed record UpdateGrantsBody(Dictionary<string, string> Grants, uint Version);
    public sealed record UpdateInfoBody(string Name, string? Description);

    public static void Map(IEndpointRouteBuilder system)
    {
        // Danh mục quyền: mọi nhân viên đã đăng nhập (trang Quyền hiệu lực của tôi, ma trận...)
        system.MapGet("/permissions/catalog", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPermissionCatalogQuery(), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff, Portal.Teacher);

        var group = system.MapGroup("/roles");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetRolesQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapPost("/", async (CreateRoleCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToCreatedResult(r => $"/api/v1/system/roles/{r.Role.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        group.MapPut("/{id:guid}/grants", async (Guid id, UpdateGrantsBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateRoleGrantsCommand(id, body.Grants, body.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPut("/{id:guid}", async (Guid id, UpdateInfoBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateRoleInfoCommand(id, body.Name, body.Description), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteRoleCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Delete);
    }
}
