using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.SendOffer;

/// <summary>TD-03 — gửi offer: lương (&gt; 0), ngày bắt đầu (≥ hôm nay); ghi ngày gửi, chuyển sang Offer.</summary>
public sealed record SendOfferCommand(Guid Id, decimal Salary, DateOnly StartDate) : ICommand<CandidateResult>;

public sealed class SendOfferHandler(IHrDbContext db, RecruitmentDesk desk, IClock clock) : ICommandHandler<SendOfferCommand, CandidateResult>
{
    public async Task<Result<CandidateResult>> Handle(SendOfferCommand cmd, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(cmd.Id, PermissionAction.Update, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var (c, job) = found.Value;
        var salary = Money.Create(cmd.Salary);
        if (salary.IsFailure) return RecruitmentErrors.OfferInvalid;
        var done = c.SendOffer(salary.Value, cmd.StartDate, clock.UtcNow, clock.Today);
        if (done.IsFailure) return done.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ResultAsync(c, job, null, [], ct);
    }
}
