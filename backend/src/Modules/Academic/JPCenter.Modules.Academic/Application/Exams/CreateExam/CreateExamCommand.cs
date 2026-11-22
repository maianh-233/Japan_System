using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.CreateExam;

/// <summary>
/// ND-04 — giáo vụ (<c>academic.classes.update</c>) tạo kỳ thi NHÁP từ 1 mốc thi của lộ trình lớp: hình thức online / giấy, giờ bắt đầu,
/// thời lượng 10–240 phút, % đạt (mặc định 60), các phần (câu đã chọn hoặc số câu để bốc). Mốc đã có kỳ thi chưa hủy → E1; thiếu câu → E2.
/// </summary>
public sealed record CreateExamCommand(Guid ClassId, ExamInput Input) : ICommand<ClassExamDto>;

public sealed class ExamInputValidator : AbstractValidator<ExamInput>
{
    public ExamInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên kỳ thi").MaximumLength(200);
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Sections).NotEmpty().WithMessage("Thêm ít nhất 1 phần thi");
    }
}

public sealed class CreateExamValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new ExamInputValidator());
}

public sealed class CreateExamHandler(IAcademicDbContext db, LearningAccess access, ExamScope scope, ExamDrafter drafter, ExamReader reader)
    : ICommandHandler<CreateExamCommand, ClassExamDto>
{
    public async Task<Result<ClassExamDto>> Handle(CreateExamCommand cmd, CancellationToken ct)
    {
        var cls = await access.ClassAsync(cmd.ClassId, false, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error;
        if (cls.Value.IsClosed) return ClassErrors.Closed;
        var input = cmd.Input;
        var cur = await scope.OfAsync(cls.Value, ct);
        var milestone = new ExamMilestoneId(input.ExamId);
        if (cur.Find(milestone) is null) return ExamScope.MilestoneNotFound;
        if (await db.ClassExams.AnyAsync(e => e.ClassId == cls.Value.Id && e.ExamId == milestone && e.Status != ClassExamStatus.Cancelled, ct))
            return ExamErrors.Duplicate;

        var sections = await drafter.BuildAsync(cur.Level, cur.Scopes[milestone], input.Sections ?? [], null, ct);
        if (sections.IsFailure) return sections.Error;
        var created = ClassExam.Create(cls.Value.Id, milestone, input.Title, input.Mode, input.StartsAt, input.DurationMinutes,
            input.PassPercent ?? ClassExam.DefaultPassPercent, input.Shuffle, sections.Value);
        if (created.IsFailure) return created.Error;

        db.ClassExams.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return await reader.OneAsync(created.Value, cls.Value, includeQuestions: true, ct);
    }
}
