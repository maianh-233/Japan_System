using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Assignments.CreateAssignment;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.UpdateAssignment;

/// <summary>ND-01.E6 — sửa bài tập; đã có HV nộp → chỉ sửa tiêu đề, mô tả, hạn nộp (đổi loại / tiêu chí → <c>academic.assignment.has_submissions</c>)</summary>
public sealed record UpdateAssignmentCommand(Guid Id, bool AsTeacher, AssignmentInput Input) : ICommand<AssignmentSavedDto>;

public sealed class UpdateAssignmentValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new AssignmentInputValidator());
}

public sealed class UpdateAssignmentHandler(IAcademicDbContext db, LearningAccess access, AssignmentEditor editor, AssignmentReader reader)
    : ICommandHandler<UpdateAssignmentCommand, AssignmentSavedDto>
{
    public async Task<Result<AssignmentSavedDto>> Handle(UpdateAssignmentCommand cmd, CancellationToken ct)
    {
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == new AssignmentId(cmd.Id), ct);
        if (a is null) return AssignmentErrors.NotFound;
        var cls = await access.ClassAsync(a.ClassId.Value, cmd.AsTeacher, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error.Type == ErrorType.NotFound ? AssignmentErrors.NotFound : cls.Error;

        var input = cmd.Input;
        var prep = await editor.PrepareAsync(cls.Value, input, a.Attachments, ct);
        if (prep.IsFailure) return prep.Error;
        var p = prep.Value;
        var hasSubmissions = await db.Submissions.AnyAsync(s => s.AssignmentId == a.Id, ct);
        var reconfigured = a.Reconfigure(input.Type, p.LessonId, input.MaxScore ?? a.MaxScore, p.Quiz, p.Attachments, hasSubmissions);
        if (reconfigured.IsFailure) return reconfigured.Error;
        var edited = a.Edit(input.Title, input.Description, input.DueDate);
        if (edited.IsFailure) return edited.Error;

        await db.SaveChangesAsync(ct);
        return new AssignmentSavedDto(await reader.OneAsync(a, ct), p.Warning);
    }
}
