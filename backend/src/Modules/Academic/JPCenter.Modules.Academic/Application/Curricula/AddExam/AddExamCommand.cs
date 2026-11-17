using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.AddExam;

/// <summary>LT-08 — mốc thi giữa kỳ / cuối kỳ, thi sau 1 bài (chỉ đánh dấu, đề tạo khi tổ chức thi)</summary>
public sealed record AddExamCommand(Guid CurriculumId, ExamKind Kind, string Title, Guid AfterLessonId, string? Note) : ICommand<CurriculumDto>;

public sealed class AddExamValidator : AbstractValidator<AddExamCommand>
{
    public AddExamValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên mốc thi").MaximumLength(300);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class AddExamHandler(CurriculumEditor editor) : ICommandHandler<AddExamCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(AddExamCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId,
            c => c.AddExam(cmd.Kind, cmd.Title, new LessonId(cmd.AfterLessonId), cmd.Note) is { IsFailure: true } r ? r.Error : Result.Success(),
            "Thêm mốc thi", ct);
}
