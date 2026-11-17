using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Users.AddOverride;
using JPCenter.Modules.System.Application.Users.GetEffectivePermissions;
using JPCenter.Modules.System.Application.Users.GetEmployeesWithoutAccount;
using JPCenter.Modules.System.Application.Users.GetUsers;
using JPCenter.Modules.System.Application.Users.InviteUser;
using JPCenter.Modules.System.Application.Users.LockUser;
using JPCenter.Modules.System.Application.Users.RemoveOverride;
using JPCenter.Modules.System.Application.Users.ResendInvite;
using JPCenter.Modules.System.Application.Users.SetUserRoles;
using JPCenter.Modules.System.Application.Users.UnlockUser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>PQ-05 → PQ-08 — phân quyền người dùng, cấp / khóa tài khoản. Quyền: Hệ thống › Phân quyền người dùng.</summary>
public static class UserPermissionEndpoints
{
    private const string Page = "system.user-permissions";

    public sealed record RolesBody(List<RoleAssignmentInput> Roles);
    public sealed record OverrideBody(string Perm, string Effect, string? Scope, DateTimeOffset? Until, string Reason);
    public sealed record LockBody(string Reason);

    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/users");

        group.MapGet("/", async (Guid? roleId, Guid? branchId, string? status, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetUsersQuery(roleId, branchId, status), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapGet("/employees-without-account", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetEmployeesWithoutAccountQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapGet("/{id:guid}/effective-permissions", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetEffectivePermissionsQuery(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        // Xem trước quyền hiệu lực nếu đổi vai trò (PQ-05 bước 3) — chỉ đọc
        group.MapPost("/{id:guid}/effective-permissions/preview", async (Guid id, RolesBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetEffectivePermissionsQuery(id, body.Roles), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapPost("/", async (InviteUserCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToCreatedResult(r => $"/api/v1/system/users/{r.User.AccountId}"))
            .RequirePermission(Page, PermissionAction.Create)
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive);

        group.MapPut("/{id:guid}/roles", async (Guid id, RolesBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetUserRolesCommand(id, body.Roles), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/overrides", async (Guid id, OverrideBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AddOverrideCommand(id, body.Perm, body.Effect, body.Scope, body.Until, body.Reason), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapDelete("/{id:guid}/overrides/{overrideId:guid}", async (Guid id, Guid overrideId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoveOverrideCommand(id, overrideId), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/resend-invite", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ResendInviteCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update)
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive);

        group.MapPost("/{id:guid}/lock", async (Guid id, LockBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new LockUserCommand(id, body.Reason), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        group.MapPost("/{id:guid}/unlock", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UnlockUserCommand(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
