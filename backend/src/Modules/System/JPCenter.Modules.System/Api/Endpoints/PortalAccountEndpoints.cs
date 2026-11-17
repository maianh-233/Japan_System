using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.PortalAccounts.CreatePortalAccount;
using JPCenter.Modules.System.Application.PortalAccounts.GetPortalAccounts;
using JPCenter.Modules.System.Application.PortalAccounts.ResetPortalPassword;
using JPCenter.Modules.System.Application.PortalAccounts.SetPortalAccountLock;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>AUTH-10 — giáo vụ cấp / quản lý tài khoản cổng HV / PH. Quyền: Học viên › Hồ sơ (phụ huynh cần thêm Liên hệ).</summary>
public static class PortalAccountEndpoints
{
    private const string Page = "students.profiles";

    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/portal-accounts");

        group.MapGet("/", async (string? ownerType, Guid? ownerId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPortalAccountsQuery(ownerType, ownerId), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapPost("/", async (CreatePortalAccountCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToCreatedResult(r => $"/api/v1/system/portal-accounts/{r.Account.Id}"))
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ResetPortalPasswordCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/lock", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetPortalAccountLockCommand(id, true), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/unlock", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetPortalAccountLockCommand(id, false), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
