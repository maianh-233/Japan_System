using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.GetCandidates;

/// <summary>TD-02 — ứng viên của 1 tin (Kanban), hoặc mọi tin trong phạm vi khi bỏ trống <paramref name="JobId"/>. Mới ứng tuyển trước.</summary>
public sealed record GetCandidatesQuery(Guid? JobId) : IQuery<IReadOnlyList<CandidateDto>>;

public sealed class GetCandidatesHandler(IHrDbContext db, RecruitmentDesk desk) : IQueryHandler<GetCandidatesQuery, IReadOnlyList<CandidateDto>>
{
    public async Task<Result<IReadOnlyList<CandidateDto>>> Handle(GetCandidatesQuery q, CancellationToken ct)
    {
        var jobIds = (await db.Jobs.AsNoTracking().ToListAsync(ct))
            .Where(j => desk.InScope(j, PermissionAction.View) && (q.JobId is not { } only || j.Id.Value == only))
            .Select(j => j.Id).ToList();
        if (q.JobId is not null && jobIds.Count == 0) return RecruitmentErrors.JobNotFound;

        var candidates = await db.Candidates.AsNoTracking().Where(c => jobIds.Contains(c.JobId)).OrderByDescending(c => c.AppliedAt).ToListAsync(ct);
        var names = await desk.NamesAsync(candidates.Where(c => c.InterviewerId != null).Select(c => c.InterviewerId!.Value), ct);
        return candidates.Select(c => RecruitmentDesk.ToDto(c, names)).ToList();
    }
}
