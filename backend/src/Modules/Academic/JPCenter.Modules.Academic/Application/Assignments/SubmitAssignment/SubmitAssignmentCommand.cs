using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.SubmitAssignment;

/// <summary>
/// ND-02 — HV nộp bài upload: ≥ 1 tệp (tải trước qua <c>/files</c>, ≤ 5 tệp) hoặc bài làm dạng chữ (E1). Sau hạn vẫn nhận, đánh dấu nộp muộn (E2).
/// Nộp lại thay toàn bộ bài khi GV chưa chấm (E5). HV đã nghỉ / chuyển lớp → E6.
/// </summary>
public sealed record SubmitAssignmentCommand(Guid AssignmentId, IReadOnlyList<SubmissionFileDto>? Files, string? Content) : ICommand<SubmissionDto>;

public sealed class SubmitAssignmentValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentValidator() => RuleFor(x => x.Content).MaximumLength(10000);
}

public sealed class SubmitAssignmentHandler(IAcademicDbContext db, LearningAccess access, LearningFiles files, AssignmentReader reader, IClock clock)
    : ICommandHandler<SubmitAssignmentCommand, SubmissionDto>
{
    public async Task<Result<SubmissionDto>> Handle(SubmitAssignmentCommand cmd, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new AssignmentId(cmd.AssignmentId), ct);
        if (a is null || !learner.Value.Enrolled(a.ClassId.Value)) return AssignmentErrors.NotFound;
        if (a.Type != AssignmentType.Upload) return AssignmentErrors.WrongType;
        if (!learner.Value.Studies(a.ClassId.Value)) return LearningAccess.NotInClass;

        var me = learner.Value.StudentId;
        var existing = await db.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == a.Id && s.StudentId == me, ct);
        var attached = await files.ToAttachedAsync(cmd.Files, ct, existing?.Files);
        if (attached.IsFailure) return attached.Error;

        var now = clock.UtcNow;
        Submission s;
        if (existing is null)
        {
            var created = Submission.SubmitFiles(a.Id, me, attached.Value, cmd.Content, now);
            if (created.IsFailure) return created.Error;
            s = created.Value;
            db.Submissions.Add(s);
        }
        else
        {
            var r = existing.Resubmit(attached.Value, cmd.Content, now);
            if (r.IsFailure) return r.Error;
            s = existing;
        }
        await db.SaveChangesAsync(ct);
        return await reader.OneAsync(s, a, ct);
    }
}
