using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.AddLesson;

/// <summary>LT-05 — thêm bài học cuối danh sách (chỉ bản nháp)</summary>
public sealed record AddLessonCommand(Guid CurriculumId, string Title, bool HasQuiz) : ICommand<CurriculumDto>;

public sealed class AddLessonValidator : AbstractValidator<AddLessonCommand>
{
    public AddLessonValidator() => RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên bài học").MaximumLength(300);
}

public sealed class AddLessonHandler(CurriculumEditor editor) : ICommandHandler<AddLessonCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(AddLessonCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.AddLesson(cmd.Title, cmd.HasQuiz) is { IsFailure: true } r ? r.Error : Result.Success(),
            "Thêm bài học", ct);
}
