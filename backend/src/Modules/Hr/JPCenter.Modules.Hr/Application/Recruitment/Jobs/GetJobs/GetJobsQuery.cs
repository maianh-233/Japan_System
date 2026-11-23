using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Jobs.GetJobs;

/// <summary>TD-01 — danh sách tin tuyển trong phạm vi <c>hr.recruitment.view</c> (đang tuyển trước, hạn gần trước).</summary>
public sealed record GetJobsQuery(Guid? BranchId, JobStatus? Status) : IQuery<IReadOnlyList<JobDto>>;

public sealed class GetJobsHandler(IHrDbContext db, RecruitmentDesk desk) : IQueryHandler<GetJobsQuery, IReadOnlyList<JobDto>>
{
    public async Task<Result<IReadOnlyList<JobDto>>> Handle(GetJobsQuery q, CancellationToken ct)
    {
        var jobs = (await db.Jobs.AsNoTracking().ToListAsync(ct))
            .Where(j => desk.InScope(j, PermissionAction.View))
            .Where(j => q.BranchId is not { } b || j.BranchId.Value == b)
            .Where(j => q.Status is not { } s || j.Status == s)
            .OrderBy(j => j.Status).ThenBy(j => j.Deadline)
            .ToList();
        var ids = jobs.Select(j => j.Id).ToList();
        var stages = (await db.Candidates.AsNoTracking().Where(c => ids.Contains(c.JobId)).Select(c => new { c.JobId, c.Stage }).ToListAsync(ct))
            .ToLookup(x => x.JobId, x => x.Stage);
        return jobs.Select(j => RecruitmentDesk.ToDto(j, stages[j.Id].ToList())).ToList();
    }
}
