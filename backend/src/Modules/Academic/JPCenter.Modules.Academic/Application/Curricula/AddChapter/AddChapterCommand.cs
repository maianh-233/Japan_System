using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.AddChapter;

/// <summary>LT-05 — thêm chapter cuối bài</summary>
public sealed record AddChapterCommand(Guid CurriculumId, Guid LessonId, string Title) : ICommand<CurriculumDto>;

public sealed class AddChapterValidator : AbstractValidator<AddChapterCommand>
{
    public AddChapterValidator() => RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên chapter").MaximumLength(300);
}

public sealed class AddChapterHandler(CurriculumEditor editor) : ICommandHandler<AddChapterCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(AddChapterCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId,
            c => c.AddChapter(new LessonId(cmd.LessonId), cmd.Title) is { IsFailure: true } r ? r.Error : Result.Success(), "Thêm chapter", ct);
}
