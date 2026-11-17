using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.AddLinkMaterial;

/// <summary>LT-06 — tài liệu dạng link: video (YouTube không công khai), Google Drive, web khác. Chỉ nhận https</summary>
public sealed record AddLinkMaterialCommand(Guid CurriculumId, Guid LessonId, Guid ChapterId, string Name, MaterialType Type, string Url,
    bool SharedWithStudents) : ICommand<CurriculumDto>;

public sealed class AddLinkMaterialValidator : AbstractValidator<AddLinkMaterialCommand>
{
    public AddLinkMaterialValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên tài liệu").MaximumLength(300);
        RuleFor(x => x.Url).NotEmpty().WithMessage("Dán đường dẫn").MaximumLength(2000);
    }
}

public sealed class AddLinkMaterialHandler(CurriculumEditor editor) : ICommandHandler<AddLinkMaterialCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(AddLinkMaterialCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId,
            c => c.AddLinkMaterial(new LessonId(cmd.LessonId), new ChapterId(cmd.ChapterId), cmd.Name, cmd.Type, cmd.Url, cmd.SharedWithStudents)
                is { IsFailure: true } r ? r.Error : Result.Success(),
            "Thêm link tài liệu", ct);
}
