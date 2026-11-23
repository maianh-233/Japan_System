using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Recruitment.Jobs.ChangeJobStatus;

/// <summary>Thao tác trạng thái tin tuyển</summary>
public enum JobAction
{
    /// <summary>Đang tuyển → Tạm dừng</summary>
    Pause,
    /// <summary>Tạm dừng / Đã đóng → Đang tuyển</summary>
    Reopen,
    /// <summary>→ Đã đóng</summary>
    Close,
}

/// <summary>TD-01 — Đang tuyển ↔ Tạm dừng → Đã đóng (mở lại được).</summary>
public sealed record ChangeJobStatusCommand(Guid Id, JobAction Action) : ICommand<JobDto>;

public sealed class ChangeJobStatusHandler(IHrDbContext db, RecruitmentDesk desk) : ICommandHandler<ChangeJobStatusCommand, JobDto>
{
    public async Task<Result<JobDto>> Handle(ChangeJobStatusCommand cmd, CancellationToken ct)
    {
        var job = await desk.FindJobAsync(cmd.Id, PermissionAction.Update, track: true, ct);
        if (job is null) return RecruitmentErrors.JobNotFound;
        var done = cmd.Action switch
        {
            JobAction.Pause => job.Pause(),
            JobAction.Reopen => job.Reopen(),
            _ => job.Close(),
        };
        if (done.IsFailure) return done.Error;
        await db.SaveChangesAsync(ct);
        return await desk.JobDtoAsync(job, ct);
    }
}
