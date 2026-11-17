using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.GetCurriculum;

/// <summary>LT-01 — 1 lộ trình kèm nội dung</summary>
public sealed record GetCurriculumQuery(Guid Id) : IQuery<CurriculumDto>;

public sealed class GetCurriculumHandler(CurriculumReader reader) : IQueryHandler<GetCurriculumQuery, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(GetCurriculumQuery query, CancellationToken ct) =>
        await reader.FindAsync(new CurriculumId(query.Id), ct) is { } dto ? dto : CurriculumErrors.NotFound;
}
