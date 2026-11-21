using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.ChangeTestStatus;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.GetAttempts;
using JPCenter.Modules.Crm.Application.PlacementTests.GetPlacementTests;
using JPCenter.Modules.Crm.Application.PlacementTests.GetTestQuestions;
using JPCenter.Modules.Crm.Application.PlacementTests.LogAttempt;
using JPCenter.Modules.Crm.Application.PlacementTests.Public;
using JPCenter.Modules.Crm.Application.PlacementTests.SavePlacementTest;
using JPCenter.Modules.Crm.Application.PlacementTests.SendAttempt;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Crm.Api.Endpoints;

/// <summary>
/// CRM-12, CRM-13 — bài test xếp lớp + lượt làm (quyền <c>crm.placement-tests</c>) và trang làm bài CÔNG KHAI theo token
/// (<c>/api/v1/crm/public/placement/{token}</c> — không đăng nhập, giới hạn tần suất theo IP — E6).
/// </summary>
public static class PlacementEndpoints
{
    private const string Page = CrmAccess.PlacementTests;

    public sealed record TestBody(string? Name, string? Description, List<SectionDto>? Sections, int DurationMin, List<BandDto>? Bands,
        bool Redraw = false, uint Version = 0);

    public sealed record StatusBody(PlacementTestStatus Status);

    public sealed record SendBody(Guid TestId, Guid LeadId, ContactChannel Channel, int ValidDays = 3);

    public sealed record AnswersBody(Dictionary<Guid, int>? Answers);

    public static void Map(IEndpointRouteBuilder crm)
    {
        var tests = crm.MapGroup("/placement-tests");

        tests.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetPlacementTestsQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        tests.MapGet("/{id:guid}/questions", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetTestQuestionsQuery(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        tests.MapPost("/", async (TestBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SavePlacementTestCommand(null, b.Name, b.Description, b.Sections ?? [], b.DurationMin, b.Bands ?? [], true, 0), ct))
                .ToCreatedResult(t => $"/api/v1/crm/placement-tests/{t.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        tests.MapPut("/{id:guid}", async (Guid id, TestBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SavePlacementTestCommand(id, b.Name, b.Description, b.Sections ?? [], b.DurationMin, b.Bands ?? [], b.Redraw,
                    b.Version), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        tests.MapPost("/{id:guid}/status", async (Guid id, StatusBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeTestStatusCommand(id, b.Status), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        var attempts = crm.MapGroup("/placement-attempts");

        attempts.MapGet("/", async (Guid? testId, Guid? leadId, QueryEnum<AttemptStatus>? status, ISender s, CancellationToken ct) =>
                (await s.Send(new GetAttemptsQuery(testId, leadId, status?.Value), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        // Gửi link: Sale có quyền "thêm" trên trang Test xếp lớp (vai trò mẫu) hoặc "sửa"
        attempts.MapPost("/", async (SendBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SendAttemptCommand(b.TestId, b.LeadId, b.Channel, b.ValidDays), ct))
                .ToCreatedResult(a => $"/api/v1/crm/placement-attempts/{a.Id}"))
            .RequireAnyPermission((Page, PermissionAction.Create), (Page, PermissionAction.Update))
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive);

        attempts.MapPost("/{id:guid}/log", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new LogAttemptCommand(id), ct)).ToHttpResult())
            .RequireAnyPermission((Page, PermissionAction.Create), (Page, PermissionAction.Update));

        // ---- Công khai (lead làm bài, không đăng nhập) ----
        var pub = crm.MapGroup("/public/placement/{token}").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Anonymous);

        pub.MapGet("/", async (string token, ISender s, CancellationToken ct) => (await s.Send(new GetPublicAttemptQuery(token), ct)).ToHttpResult());

        pub.MapPost("/start", async (string token, ISender s, CancellationToken ct) =>
            (await s.Send(new StartPublicAttemptCommand(token), ct)).ToHttpResult());

        pub.MapPut("/answers", async (string token, AnswersBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SavePublicAnswersCommand(token, b.Answers), ct)).ToHttpResult());

        pub.MapPost("/submit", async (string token, AnswersBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitPublicAttemptCommand(token, b.Answers), ct)).ToHttpResult());
    }
}
