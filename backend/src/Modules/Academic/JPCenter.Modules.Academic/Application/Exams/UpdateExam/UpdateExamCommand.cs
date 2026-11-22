using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Exams.CreateExam;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.UpdateExam;

/// <summary>
/// ND-04 — sửa kỳ thi: nháp → đổi mọi thứ (đề, giờ, hình thức; không đổi mốc thi); đã phát hành → chỉ đổi giờ thi khi chưa ai làm bài (E4).
/// </summary>
public sealed record UpdateExamCommand(Guid Id, ExamInput Input) : ICommand<ClassExamDto>;

public sealed class UpdateExamValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new ExamInputValidator());
}

public sealed class UpdateExamHandler(IAcademicDbContext db, ExamFinder finder, ExamScope scope, ExamDrafter drafter, ExamReader reader)
    : ICommandHandler<UpdateExamCommand, ClassExamDto>
{
    public static readonly Error MilestoneLocked = Error.Validation("academic.exam.milestone_locked", "Không đổi mốc thi của kỳ thi — hủy và tạo kỳ thi mới");

    public async Task<Result<ClassExamDto>> Handle(UpdateExamCommand cmd, CancellationToken ct)
    {
        var found = await finder.FindAsync(cmd.Id, false, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var (exam, cls) = found.Value;
        var input = cmd.Input;
        if (input.ExamId != exam.ExamId.Value) return MilestoneLocked;

        Result r;
        if (exam.Status == ClassExamStatus.Published)
        {
            var hasAttempts = await db.ExamAttempts.AnyAsync(a => a.ClassExamId == exam.Id, ct);
            r = exam.Reschedule(input.StartsAt, input.DurationMinutes, hasAttempts);
        }
        else
        {
            var cur = await scope.OfAsync(cls, ct);
            var sections = await drafter.BuildAsync(cur.Level, cur.Scopes.GetValueOrDefault(exam.ExamId) ?? [], input.Sections ?? [], null, ct);
            if (sections.IsFailure) return sections.Error;
            r = exam.UpdateDraft(input.Title, input.Mode, input.StartsAt, input.DurationMinutes, input.PassPercent ?? exam.PassPercent, input.Shuffle,
                sections.Value);
        }
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return await reader.OneAsync(exam, cls, includeQuestions: true, ct);
    }
}
