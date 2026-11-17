using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.UpdateMaterial;

/// <summary>LT-06 — đổi tên hiển thị / cờ "Học viên xem được"</summary>
public sealed record UpdateMaterialCommand(Guid CurriculumId, Guid MaterialId, string Name, bool SharedWithStudents) : ICommand<CurriculumDto>;

public sealed class UpdateMaterialValidator : AbstractValidator<UpdateMaterialCommand>
{
    public UpdateMaterialValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên tài liệu").MaximumLength(300);
}

public sealed class UpdateMaterialHandler(CurriculumEditor editor) : ICommandHandler<UpdateMaterialCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(UpdateMaterialCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.UpdateMaterial(new MaterialId(cmd.MaterialId), cmd.Name, cmd.SharedWithStudents),
            "Sửa tài liệu", ct);
}
