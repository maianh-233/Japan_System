using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.UpdateExam;

/// <summary>LT-08 — sửa mốc thi (loại, tên, thi sau bài, ghi chú)</summary>
public sealed record UpdateExamCommand(Guid CurriculumId, Guid ExamId, ExamKind Kind, string Title, Guid AfterLessonId, string? Note)
    : ICommand<CurriculumDto>;

public sealed class UpdateExamValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên mốc thi").MaximumLength(300);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class UpdateExamHandler(CurriculumEditor editor) : ICommandHandler<UpdateExamCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(UpdateExamCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId,
            c => c.UpdateExam(new ExamMilestoneId(cmd.ExamId), cmd.Kind, cmd.Title, new LessonId(cmd.AfterLessonId), cmd.Note), "Sửa mốc thi", ct);
}
