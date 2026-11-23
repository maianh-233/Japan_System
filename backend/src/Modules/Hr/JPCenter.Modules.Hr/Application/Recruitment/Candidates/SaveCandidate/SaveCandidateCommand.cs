using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.SaveCandidate;

/// <summary>
/// TD-02 — thêm ứng viên vào tin (<paramref name="Id"/> null, cần <paramref name="JobId"/>) hoặc sửa thông tin ứng viên.
/// Tin đã đóng → <c>hr.job.closed</c> (E1); cùng SĐT / email đã ứng tuyển tin này → <c>hr.candidate.duplicate</c> (E2).
/// </summary>
public sealed record SaveCandidateCommand(Guid? Id, Guid? JobId, CandidateInput Input) : ICommand<CandidateResult>;

public sealed class SaveCandidateValidator : AbstractValidator<SaveCandidateCommand>
{
    public SaveCandidateValidator()
    {
        RuleFor(x => x.Input).NotNull().SetValidator(new CandidateInputValidator());
        RuleFor(x => x.JobId).NotNull().When(x => x.Id is null).WithMessage("Chọn tin tuyển");
    }
}

public sealed class SaveCandidateHandler(IHrDbContext db, RecruitmentDesk desk, IClock clock) : ICommandHandler<SaveCandidateCommand, CandidateResult>
{
    public async Task<Result<CandidateResult>> Handle(SaveCandidateCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var salary = RecruitmentDesk.Vnd(i.ExpectedSalary);
        if (salary.IsFailure) return salary.Error;
        var phone = PhoneNumber.Normalize(i.Phone) ?? i.Phone.Trim();
        var email = i.Email.Trim().ToLowerInvariant();

        Candidate candidate;
        Job job;
        if (cmd.Id is { } id)
        {
            var found = await desk.FindCandidateAsync(id, PermissionAction.Update, ct);
            if (found is null) return RecruitmentErrors.CandidateNotFound;
            (candidate, job) = found.Value;
            if (await IsDuplicateAsync(job.Id, phone, email, candidate.Id, ct)) return RecruitmentErrors.Duplicate;
            var updated = candidate.UpdateInfo(i.FullName, phone, email, i.Source, i.Japanese, salary.Value);
            if (updated.IsFailure) return updated.Error;
        }
        else
        {
            var foundJob = await desk.FindJobAsync(cmd.JobId!.Value, PermissionAction.Update, track: false, ct);
            if (foundJob is null) return RecruitmentErrors.JobNotFound;
            job = foundJob;
            if (job.Status == JobStatus.Closed) return RecruitmentErrors.JobClosed;
            if (await IsDuplicateAsync(job.Id, phone, email, null, ct)) return RecruitmentErrors.Duplicate;
            var created = Candidate.Apply(job.Id, i.FullName, phone, email, i.Source, clock.UtcNow, i.Japanese, salary.Value);
            if (created.IsFailure) return created.Error;
            candidate = created.Value;
            db.Candidates.Add(candidate);
        }

        await db.SaveChangesAsync(ct);
        return await desk.ResultAsync(candidate, job, null, [], ct);
    }

    private async Task<bool> IsDuplicateAsync(JobId jobId, string phone, string email, CandidateId? except, CancellationToken ct)
    {
        var same = await db.Candidates.AsNoTracking().Where(c => c.JobId == jobId && (c.Phone == phone || c.Email == email))
            .Select(c => c.Id).ToListAsync(ct);
        return same.Any(x => x != except);
    }
}
