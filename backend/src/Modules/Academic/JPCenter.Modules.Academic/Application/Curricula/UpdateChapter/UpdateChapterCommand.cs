using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.UpdateChapter;

/// <summary>LT-05 — đổi tên chapter</summary>
public sealed record UpdateChapterCommand(Guid CurriculumId, Guid LessonId, Guid ChapterId, string Title) : ICommand<CurriculumDto>;

public sealed class UpdateChapterValidator : AbstractValidator<UpdateChapterCommand>
{
    public UpdateChapterValidator() => RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên chapter").MaximumLength(300);
}

public sealed class UpdateChapterHandler(CurriculumEditor editor) : ICommandHandler<UpdateChapterCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(UpdateChapterCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.RenameChapter(new LessonId(cmd.LessonId), new ChapterId(cmd.ChapterId), cmd.Title),
            "Sửa chapter", ct);
}
