using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.GetPromotions;

/// <summary>DT-17 bước 1 — tab Lên lớp của chi tiết lớp: HV trong danh sách lên lớp, lớp đích, giữ chỗ, quan tâm, tiến độ tư vấn</summary>
public sealed record GetPromotionsQuery(Guid ClassId) : IQuery<IReadOnlyList<PromotionDto>>;

public sealed class GetPromotionsHandler(IAcademicDbContext db, ClassReader classes, PromotionReader reader)
    : IQueryHandler<GetPromotionsQuery, IReadOnlyList<PromotionDto>>
{
    public async Task<Result<IReadOnlyList<PromotionDto>>> Handle(GetPromotionsQuery q, CancellationToken ct)
    {
        if (await classes.ForUpdateAsync(q.ClassId, AcademicAccess.Classes, PermissionAction.View, ct) is null) return ClassErrors.NotFound;
        var list = await db.PromotionCandidates.AsNoTracking().Where(p => p.FromClassId == new ClassId(q.ClassId)).ToListAsync(ct);
        return Result.Success(await reader.ToDtosAsync(list, ct));
    }
}
