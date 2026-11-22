using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Learning.GetClassCurriculum;

/// <summary>
/// Cổng GV: lộ trình của lớp mình dạy (bài học, mốc thi) — GV không có quyền trang Lộ trình nhưng cần chọn bài khi giao bài tập (ND-01).
/// Lớp không gắn lộ trình → 404.
/// </summary>
public sealed record GetClassCurriculumQuery(Guid ClassId) : IQuery<CurriculumDto>;

public sealed class GetClassCurriculumHandler(LearningAccess access, CurriculumReader reader) : IQueryHandler<GetClassCurriculumQuery, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(GetClassCurriculumQuery q, CancellationToken ct)
    {
        var cls = await access.ClassAsync(q.ClassId, asTeacher: true, PermissionAction.View, ct);
        if (cls.IsFailure) return cls.Error;
        if (cls.Value.CurriculumId is not { } id || await reader.FindAsync(id, ct) is not { } dto) return CurriculumErrors.NotFound;
        return dto;
    }
}
