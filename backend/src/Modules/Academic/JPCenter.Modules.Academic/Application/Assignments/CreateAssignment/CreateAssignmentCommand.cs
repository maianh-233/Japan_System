using JPCenter.Application.Common.Time;
using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Assignments.CreateAssignment;

/// <summary>
/// ND-01 — giao bài tập cho lớp (giáo vụ <c>academic.classes.update</c> hoặc GV của lớp): upload (đề / tài liệu đính kèm) hoặc quiz
/// (tiêu chí bốc câu đã duyệt). Ngân hàng thiếu câu đúng bài → vẫn giao kèm cảnh báo (E4). Báo chuông "Bài tập mới" cho HV đang học.
/// </summary>
public sealed record CreateAssignmentCommand(Guid ClassId, bool AsTeacher, AssignmentInput Input) : ICommand<AssignmentSavedDto>;

public sealed class AssignmentInputValidator : AbstractValidator<AssignmentInput>
{
    public AssignmentInputValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên bài tập").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.Attachments).Must(a => a is null || a.Count <= 10).WithMessage("Đính kèm tối đa 10 tệp");
    }
}

public sealed class CreateAssignmentValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new AssignmentInputValidator());
}

public sealed class CreateAssignmentHandler(IAcademicDbContext db, LearningAccess access, AssignmentEditor editor, AssignmentReader reader,
    LearningNotices notices, IClock clock) : ICommandHandler<CreateAssignmentCommand, AssignmentSavedDto>
{
    public async Task<Result<AssignmentSavedDto>> Handle(CreateAssignmentCommand cmd, CancellationToken ct)
    {
        var cls = await access.ClassAsync(cmd.ClassId, cmd.AsTeacher, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error;
        var input = cmd.Input;
        var prep = await editor.PrepareAsync(cls.Value, input, null, ct);
        if (prep.IsFailure) return prep.Error;

        var p = prep.Value;
        var created = Assignment.Create(cls.Value.Id, input.Type, input.Title, p.LessonId, input.Description, clock.UtcNow, input.DueDate,
            input.MaxScore ?? AssignmentEditor.DefaultMaxScore, p.Quiz, p.Attachments, access.ActorId);
        if (created.IsFailure) return created.Error;
        var a = created.Value;
        db.Assignments.Add(a);

        var students = await access.StudyingAsync(cls.Value.Id, ct);
        await notices.SendAsync(new LearningReach(students, [], []), "Bài tập mới", $"{cls.Value.Code}: {a.Title} — hạn {TimeText.Moment(a.DueDate)}",
            "info", "academic.assignment.created", "/student/assignments", "/parent/results", "/teacher/assignments", ct);
        await db.SaveChangesAsync(ct);
        return new AssignmentSavedDto(await reader.OneAsync(a, ct), p.Warning);
    }
}
