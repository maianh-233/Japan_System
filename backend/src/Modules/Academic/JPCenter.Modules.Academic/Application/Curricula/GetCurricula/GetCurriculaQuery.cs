using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.GetCurricula;

/// <summary>LT-01 — mọi lộ trình kèm nội dung (danh mục vài chục bản; lọc / tìm ở frontend)</summary>
public sealed record GetCurriculaQuery : IQuery<IReadOnlyList<CurriculumDto>>;

public sealed class GetCurriculaHandler(CurriculumReader reader) : IQueryHandler<GetCurriculaQuery, IReadOnlyList<CurriculumDto>>
{
    public async Task<Result<IReadOnlyList<CurriculumDto>>> Handle(GetCurriculaQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(ct));
}
