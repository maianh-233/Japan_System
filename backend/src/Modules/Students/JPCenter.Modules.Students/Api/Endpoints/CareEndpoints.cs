using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.CareCases.AddCareLog;
using JPCenter.Modules.Students.Application.CareCases.GetCareCases;
using JPCenter.Modules.Students.Application.CareCases.GetCareLogs;
using JPCenter.Modules.Students.Application.CareCases.RecalculateRisk;
using JPCenter.Modules.Students.Application.CareCases.UpdateCareCase;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Students.Api.Endpoints;

/// <summary>HV-14 — Cảnh báo bỏ học & chăm sóc <c>/api/v1/students/at-risk</c>, quyền <c>students.at-risk.*</c>. Chỉ cổng nhân viên.</summary>
public static class CareEndpoints
{
    public sealed record UpdateBody(Guid? AssigneeId, CareStatus Status, DateOnly? NextAt);

    public sealed record LogBody(CareChannel Channel, string Note);

    public static void Map(IEndpointRouteBuilder students)
    {
        const string page = StudentAccess.AtRisk;
        var g = students.MapGroup("/at-risk");

        g.MapGet("/", async (Guid? branchId, Guid? classId, Guid? studentId, QueryEnum<RiskLevel>? level, QueryEnum<CareStatus>? status,
                    bool? includeResolved, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCareCasesQuery(branchId, classId, studentId, level?.Value, status?.Value, includeResolved ?? false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/recalculate", async (ISender s, CancellationToken ct) => (await s.Send(new RecalculateRiskCommand(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}", async (Guid id, UpdateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateCareCaseCommand(id, b.AssigneeId, b.Status, b.NextAt), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/logs", async (Guid id, LogBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddCareLogCommand(id, b.Channel, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // Nhật ký liên hệ: trang Cảnh báo bỏ học hoặc tab Học viên của lớp
        students.MapGet("/care-logs", async (Guid? classId, Guid? studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCareLogsQuery(classId, studentId), ct)).ToHttpResult())
            .RequireAnyPermission((page, PermissionAction.View), (StudentAccess.Classes, PermissionAction.View));
    }
}
