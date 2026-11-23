using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Tasks.GetTeamReports;

/// <summary>1 cấp dưới + số việc đang mở / quá hạn. <c>level</c>: 1 = trực tiếp</summary>
public sealed record TeamReportDto(Guid Id, string Code, string FullName, string Position, string Department, Guid BranchId, Guid? ManagerId,
    int Level, int Open, int Overdue);

/// <summary>CV-04 — cấp dưới theo cây "quản lý trực tiếp" (trực tiếp trước, rồi gián tiếp); không có cấp dưới → rỗng. Không cần mã quyền.</summary>
public sealed record GetTeamReportsQuery : IQuery<IReadOnlyList<TeamReportDto>>;

public sealed class GetTeamReportsHandler(IWorkDbContext db, WorkPeople people, IClock clock) : IQueryHandler<GetTeamReportsQuery, IReadOnlyList<TeamReportDto>>
{
    public async Task<Result<IReadOnlyList<TeamReportDto>>> Handle(GetTeamReportsQuery q, CancellationToken ct)
    {
        var reports = await people.MyReportsAsync(ct);
        var ids = reports.Select(r => r.Employee.Id).ToList();
        var today = clock.Today;
        var open = await db.Tasks.AsNoTracking().Where(t => ids.Contains(t.AssigneeId) && t.Status != Domain.Tasks.WorkTaskStatus.Done)
            .Select(t => new { t.AssigneeId, t.Due }).ToListAsync(ct);
        var byAssignee = open.ToLookup(t => t.AssigneeId);
        return reports.OrderBy(r => r.Level).ThenBy(r => r.Employee.FullName)
            .Select(r => new TeamReportDto(r.Employee.Id, r.Employee.Code, r.Employee.FullName, r.Employee.Position, r.Employee.Department,
                r.Employee.BranchId, r.Employee.ManagerId, r.Level, byAssignee[r.Employee.Id].Count(),
                byAssignee[r.Employee.Id].Count(t => t.Due < today)))
            .ToList();
    }
}
