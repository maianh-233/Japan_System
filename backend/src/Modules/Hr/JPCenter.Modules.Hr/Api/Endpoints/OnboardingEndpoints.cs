using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Onboardings.CreateEmployeeFromOnboarding;
using JPCenter.Modules.Hr.Application.Onboardings.CreateOnboarding;
using JPCenter.Modules.Hr.Application.Onboardings.GetOnboardings;
using JPCenter.Modules.Hr.Application.Onboardings.TickOnboardingItem;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Hr.Api.Endpoints;

/// <summary>OB-01, OB-02 — <c>/hr/onboardings</c>, quyền <c>hr.onboarding.*</c>. "Tạo hồ sơ" kiểm thêm quyền tạo ở trang Nhân viên / Giáo viên.</summary>
public static class OnboardingEndpoints
{
    public sealed record TickBody(bool Done);

    public sealed record EmployeeBody(Gender Gender, TeacherNationality? TeacherType, List<string>? Levels, decimal? RateOffline, decimal? RateOnline);

    public static void Map(IEndpointRouteBuilder hr)
    {
        const string page = HrScope.Onboarding;
        var g = hr.MapGroup("/onboardings");
        g.MapGet("/", async (QueryEnum<OnboardingFilter>? filter, ISender s, CancellationToken ct) =>
                (await s.Send(new GetOnboardingsQuery(filter?.Value ?? OnboardingFilter.All), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        g.MapPost("/", async (CreateOnboardingCommand b, ISender s, CancellationToken ct) =>
                (await s.Send(b, ct)).ToCreatedResult(o => $"/api/v1/hr/onboardings/{o.Id}"))
            .RequirePermission(page, PermissionAction.Create);
        g.MapPut("/{id:guid}/items/{key}", async (Guid id, string key, TickBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new TickOnboardingItemCommand(id, key, b.Done), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPost("/{id:guid}/employee", async (Guid id, EmployeeBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateEmployeeFromOnboardingCommand(id, b.Gender, b.TeacherType, b.Levels, b.RateOffline, b.RateOnline), ct))
                .ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .RequireAnyPermission(("hr.employees", PermissionAction.Create), ("hr.teachers", PermissionAction.Create));
    }
}
