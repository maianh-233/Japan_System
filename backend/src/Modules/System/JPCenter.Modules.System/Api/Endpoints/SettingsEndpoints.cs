using System.Text.Json;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Settings.GetSettings;
using JPCenter.Modules.System.Application.Settings.UpdateSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>CD-01, CD-02 — Cài đặt chung. Đọc: mọi nhân viên / GV đăng nhập (frontend cần lúc đăng nhập); sửa: Hệ thống › Cài đặt chung.</summary>
public static class SettingsEndpoints
{
    private const string Page = "system.settings";

    public sealed record SettingsBody(JsonElement Settings);

    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/settings");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetSettingsQuery(), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff, Portal.Teacher);

        group.MapPut("/", async (SettingsBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateSettingsCommand(body.Settings), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
