using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Reporting.Application.Common;
using JPCenter.Modules.Reporting.Application.Reports.Common;
using JPCenter.Modules.Reporting.Domain.Periods;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Application.Reports.ExportReport;

/// <summary>
/// BC-07 — trả toàn bộ số liệu báo cáo để frontend ghi file .xlsx (<c>lib/spreadsheet.ts</c>, backend không thêm thư viện Excel) và ghi nhật ký.
/// Là command vì bản thân việc xuất được ghi nhật ký (giống NK-02).
/// </summary>
public sealed record ExportReportCommand(Guid? BranchId, int Months) : ICommand<ReportDto>;

public sealed class ExportReportHandler(ReportingAccess access, ReportBuilder builder, IBranchDirectory branchDirectory, IAuditTrail audit, IClock clock)
    : ICommandHandler<ExportReportCommand, ReportDto>
{
    public async Task<Result<ReportDto>> Handle(ExportReportCommand cmd, CancellationToken ct)
    {
        var period = ReportPeriod.Create(cmd.Months, clock.Today);
        if (period.IsFailure) return period.Error;
        var branches = access.Branches(ReportingAccess.Reports, PermissionAction.Export, cmd.BranchId);
        Guid? one = branches is { Count: 1 } b ? b.First() : null;
        var report = await builder.BuildAsync(period.Value, branches, one, ct);

        var scope = one is { } id ? (await branchDirectory.FindAsync(id, ct))?.Name ?? "Chi nhánh" : branches is null ? "Toàn hệ thống" : $"{branches.Count} chi nhánh";
        audit.Record(AuditCategory.Export, AuditSeverity.Warning, "Xuất báo cáo", $"Báo cáo {cmd.Months} tháng · {scope}", branchId: one);
        return report;
    }
}
