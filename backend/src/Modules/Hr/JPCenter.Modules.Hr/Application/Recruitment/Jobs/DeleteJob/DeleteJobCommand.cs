using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Jobs.DeleteJob;

/// <summary>TD-01 — xóa tin vào Thùng rác (<c>hr.job</c>); chỉ khi chưa có ứng viên (<c>hr.job.has_candidates</c>).</summary>
public sealed record DeleteJobCommand(Guid Id) : ICommand;

public sealed class DeleteJobHandler(IHrDbContext db, RecruitmentDesk desk, IIntegrationEventPublisher events, ICurrentUser user, IClock clock)
    : ICommandHandler<DeleteJobCommand>
{
    public const string TrashKind = "hr.job";

    public async Task<Result> Handle(DeleteJobCommand cmd, CancellationToken ct)
    {
        var job = await desk.FindJobAsync(cmd.Id, PermissionAction.Delete, track: true, ct);
        if (job is null) return RecruitmentErrors.JobNotFound;
        if (await db.Candidates.AnyAsync(c => c.JobId == job.Id, ct)) return RecruitmentErrors.HasCandidates;

        db.Jobs.Remove(job);
        events.Publish(new ItemTrashed(TrashKind, job.Id.Value, job.Title, $"Tin tuyển · {job.Quantity} người · hạn {job.Deadline:dd/MM/yyyy}",
            job.BranchId.Value, user.AccountId ?? Guid.Empty, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
