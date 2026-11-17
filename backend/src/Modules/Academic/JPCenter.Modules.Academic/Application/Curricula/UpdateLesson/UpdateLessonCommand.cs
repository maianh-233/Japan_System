using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.UpdateLesson;

/// <summary>LT-05 — đổi tên bài / bật tắt bài kiểm tra cuối bài</summary>
public sealed record UpdateLessonCommand(Guid CurriculumId, Guid LessonId, string Title, bool HasQuiz) : ICommand<CurriculumDto>;

public sealed class UpdateLessonValidator : AbstractValidator<UpdateLessonCommand>
{
    public UpdateLessonValidator() => RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên bài học").MaximumLength(300);
}

public sealed class UpdateLessonHandler(CurriculumEditor editor) : ICommandHandler<UpdateLessonCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(UpdateLessonCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.UpdateLesson(new LessonId(cmd.LessonId), cmd.Title, cmd.HasQuiz), "Sửa bài học", ct);
}
