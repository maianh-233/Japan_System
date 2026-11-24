using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Reporting.Application.Common;
using JPCenter.Modules.Reporting.Application.Reports.Common;
using JPCenter.Modules.Reporting.Domain.Periods;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Application.Reports.GetReport;

/// <summary>BC-03 → 06 — báo cáo N tháng (3 / 6 / 12) của chi nhánh đang chọn (null = toàn bộ phạm vi).</summary>
public sealed record GetReportQuery(Guid? BranchId, int Months) : IQuery<ReportDto>;

public sealed class GetReportHandler(ReportingAccess access, ReportBuilder builder, IClock clock) : IQueryHandler<GetReportQuery, ReportDto>
{
    public async Task<Result<ReportDto>> Handle(GetReportQuery q, CancellationToken ct)
    {
        var period = ReportPeriod.Create(q.Months, clock.Today);
        if (period.IsFailure) return period.Error;
        var branches = access.Branches(ReportingAccess.Reports, PermissionAction.View, q.BranchId);
        return await builder.BuildAsync(period.Value, branches, branches is { Count: 1 } one ? one.First() : null, ct);
    }
}
