using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Reporting.Application.Common;
using JPCenter.Modules.Reporting.Application.Dashboard.GetDashboard;
using JPCenter.Modules.Reporting.Application.Reports.ExportReport;
using JPCenter.Modules.Reporting.Application.Reports.GetReport;
using JPCenter.Modules.Reporting.Application.TodayWork.GetTodayWork;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Reporting.Api;

/// <summary><c>/api/v1/reporting</c> — chỉ đọc (user story 17). Phạm vi chi nhánh lấy theo quyền, <c>branchId</c> = chi nhánh đang chọn.</summary>
public static class ReportingEndpoints
{
    public const int DefaultMonths = 6;

    public sealed record ExportBody(Guid? BranchId, int? Months);

    public static void Map(IEndpointRouteBuilder reporting)
    {
        // BC-01
        reporting.MapGet("/dashboard", async (Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetDashboardQuery(branchId), ct)).ToHttpResult())
            .RequirePermission(ReportingAccess.Overview, PermissionAction.View);

        // BC-02
        reporting.MapGet("/today-work", async (Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetTodayWorkQuery(branchId), ct)).ToHttpResult())
            .RequirePermission(ReportingAccess.Overview, PermissionAction.View);

        // BC-03 → 06
        reporting.MapGet("/reports", async (Guid? branchId, int? months, ISender s, CancellationToken ct) =>
                (await s.Send(new GetReportQuery(branchId, months ?? DefaultMonths), ct)).ToHttpResult())
            .RequirePermission(ReportingAccess.Reports, PermissionAction.View);

        // BC-07
        reporting.MapPost("/reports/export", async (ExportBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ExportReportCommand(b.BranchId, b.Months ?? DefaultMonths), ct)).ToHttpResult())
            .RequirePermission(ReportingAccess.Reports, PermissionAction.Export);
    }
}
