using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Integrations.ConnectIntegration;
using JPCenter.Modules.System.Application.Integrations.DisconnectIntegration;
using JPCenter.Modules.System.Application.Integrations.GetIntegrationLogs;
using JPCenter.Modules.System.Application.Integrations.GetIntegrations;
using JPCenter.Modules.System.Application.Integrations.SetIntegrationEvent;
using JPCenter.Modules.System.Application.Integrations.TestIntegration;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>
/// User story 15 — Hệ thống › Tích hợp (<c>system.integrations</c>: <c>view</c> xem / nhật ký, <c>update</c> kết nối / ngắt / bật việc / kiểm tra).
/// Mọi lần sửa cấu hình ghi Nhật ký hệ thống nhóm <c>setting</c> (quy tắc vàng 2).
/// </summary>
public static class IntegrationEndpoints
{
    public const string Page = "system.integrations";

    public sealed record ConnectBody(Dictionary<string, string?>? Config, string? Account);

    public sealed record EventBody(bool On);

    public static void Map(IEndpointRouteBuilder system)
    {
        var g = system.MapGroup("/integrations");

        g.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetIntegrationsQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);
        g.MapGet("/{key}/logs", async (string key, ISender s, CancellationToken ct) =>
                (await s.Send(new GetIntegrationLogsQuery(key), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPut("/{key}/connection", async (string key, ConnectBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ConnectIntegrationCommand(key, b.Config ?? [], b.Account), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update)
            .Audited("Kết nối tích hợp", AuditCategory.Setting, AuditSeverity.Warning);
        g.MapDelete("/{key}/connection", async (string key, ISender s, CancellationToken ct) =>
                (await s.Send(new DisconnectIntegrationCommand(key), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update)
            .Audited("Ngắt kết nối tích hợp", AuditCategory.Setting, AuditSeverity.Warning);
        g.MapPut("/{key}/events/{evt}", async (string key, string evt, EventBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SetIntegrationEventCommand(key, evt, b.On), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update)
            .Audited("Bật / tắt việc tích hợp", AuditCategory.Setting);
        g.MapPost("/{key}/test", async (string key, ISender s, CancellationToken ct) =>
                (await s.Send(new TestIntegrationCommand(key), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);
    }
}
