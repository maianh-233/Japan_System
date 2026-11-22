using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.GradeSubmission;

/// <summary>
/// ND-03 — GV của lớp / giáo vụ chấm hoặc sửa điểm (0 → thang điểm, bước 0,25 — E1) + nhận xét; quiz: ghi đè điểm tự chấm.
/// Báo chuông HV "Bài tập đã được chấm".
/// </summary>
public sealed record GradeSubmissionCommand(Guid SubmissionId, bool AsTeacher, decimal Score, string? Comment) : ICommand<SubmissionDto>;

public sealed class GradeSubmissionValidator : AbstractValidator<GradeSubmissionCommand>
{
    public GradeSubmissionValidator() => RuleFor(x => x.Comment).MaximumLength(2000);
}

public sealed class GradeSubmissionHandler(IAcademicDbContext db, LearningAccess access, AssignmentReader reader, LearningNotices notices,
    IClock clock) : ICommandHandler<GradeSubmissionCommand, SubmissionDto>
{
    public async Task<Result<SubmissionDto>> Handle(GradeSubmissionCommand cmd, CancellationToken ct)
    {
        var s = await db.Submissions.FirstOrDefaultAsync(x => x.Id == new SubmissionId(cmd.SubmissionId), ct);
        if (s is null) return AssignmentErrors.SubmissionNotFound;
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.AssignmentId, ct);
        if (a is null) return AssignmentErrors.SubmissionNotFound;
        var cls = await access.ClassAsync(a.ClassId.Value, cmd.AsTeacher, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error.Type == ErrorType.NotFound ? AssignmentErrors.SubmissionNotFound : cls.Error;

        var graded = s.Grade(cmd.Score, a.MaxScore, cmd.Comment, access.ActorId, clock.UtcNow);
        if (graded.IsFailure) return graded.Error;
        await notices.SendAsync(new LearningReach([s.StudentId], [], []), "Bài tập đã được chấm",
            $"{cls.Value.Code}: {a.Title} — {s.Score:0.##}/{a.MaxScore:0.##} điểm", "success", "academic.submission.graded", "/student/assignments",
            "/parent/results", "/teacher/assignments", ct);
        await db.SaveChangesAsync(ct);
        return await reader.OneAsync(s, a, ct);
    }
}
