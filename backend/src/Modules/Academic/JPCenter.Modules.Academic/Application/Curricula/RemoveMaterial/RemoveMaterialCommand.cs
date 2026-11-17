using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.RemoveMaterial;

/// <summary>LT-06.E8 — xóa tài liệu; file chỉ bị xóa khỏi kho khi không còn tài liệu nào (bản sao, phiên bản khác) dùng chung</summary>
public sealed record RemoveMaterialCommand(Guid CurriculumId, Guid MaterialId) : ICommand<CurriculumDto>;

public sealed class RemoveMaterialHandler(CurriculumEditor editor) : ICommandHandler<RemoveMaterialCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(RemoveMaterialCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c =>
        {
            var removed = c.RemoveMaterial(new MaterialId(cmd.MaterialId));
            return removed.IsFailure ? removed.Error : Result.Success<IReadOnlyList<Material>>([removed.Value]);
        }, "Xóa tài liệu", ct);
}
