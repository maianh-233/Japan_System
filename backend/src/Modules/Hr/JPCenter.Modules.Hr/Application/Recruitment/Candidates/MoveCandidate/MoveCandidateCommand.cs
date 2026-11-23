using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.MoveCandidate;

/// <summary>
/// TD-02 — kéo thả Kanban sang Ứng tuyển / Sàng lọc, hoặc lùi về Phỏng vấn / Offer đã có thông tin (E4). Vào Phỏng vấn / Offer lần đầu,
/// Nhận việc, Loại phải qua thao tác riêng (<c>hr.candidate.stage_action</c>); hồ sơ đã đóng → E3.
/// </summary>
public sealed record MoveCandidateCommand(Guid Id, CandidateStage Stage) : ICommand<CandidateResult>;

public sealed class MoveCandidateHandler(IHrDbContext db, RecruitmentDesk desk) : ICommandHandler<MoveCandidateCommand, CandidateResult>
{
    public async Task<Result<CandidateResult>> Handle(MoveCandidateCommand cmd, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(cmd.Id, PermissionAction.Update, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var (c, job) = found.Value;
        var moved = c.MoveTo(cmd.Stage);
        if (moved.IsFailure) return moved.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ResultAsync(c, job, null, [], ct);
    }
}
