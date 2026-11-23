using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.AddCandidateNote;

/// <summary>TD-02 — ghi chú theo thời gian (người ghi), kể cả hồ sơ đã đóng.</summary>
public sealed record AddCandidateNoteCommand(Guid Id, string Text) : ICommand<CandidateResult>;

public sealed class AddCandidateNoteValidator : AbstractValidator<AddCandidateNoteCommand>
{
    public AddCandidateNoteValidator() => RuleFor(x => x.Text).MaximumLength(2000);
}

public sealed class AddCandidateNoteHandler(IHrDbContext db, RecruitmentDesk desk, CurrentEmployee current, IClock clock)
    : ICommandHandler<AddCandidateNoteCommand, CandidateResult>
{
    public async Task<Result<CandidateResult>> Handle(AddCandidateNoteCommand cmd, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(cmd.Id, PermissionAction.Update, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var (c, job) = found.Value;
        var added = c.AddNote(cmd.Text, current.Id ?? Guid.Empty, current.Name, clock.UtcNow);
        if (added.IsFailure) return added.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ResultAsync(c, job, null, [], ct);
    }
}
