using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.GetSalePromotions;

/// <summary>Đề xuất lên lớp phía Sale + người quyết định (PH liên hệ chính — null = chính HV quyết định)</summary>
public sealed record SalePromotionDto(PromotionDto Promotion, GuardianContact? Decider);

/// <summary>
/// CRM-19 bước 1–2 — danh sách đề xuất đã giao Sale (phạm vi own = của mình) kèm người quyết định để gom các con của cùng 1 PH.
/// </summary>
public sealed record GetSalePromotionsQuery : IQuery<IReadOnlyList<SalePromotionDto>>;

public sealed class GetSalePromotionsHandler(IAcademicDbContext db, SalePromotions sale, PromotionReader reader, IGuardianDirectory guardians)
    : IQueryHandler<GetSalePromotionsQuery, IReadOnlyList<SalePromotionDto>>
{
    public async Task<Result<IReadOnlyList<SalePromotionDto>>> Handle(GetSalePromotionsQuery query, CancellationToken ct)
    {
        var list = await sale.Scope(db.PromotionCandidates.AsNoTracking(), PermissionAction.View).ToListAsync(ct);
        var dtos = await reader.ToDtosAsync(list, ct);
        var deciders = await guardians.DecidersOfAsync(list.Select(p => p.StudentId).ToList(), ct);
        return dtos.Select(d => new SalePromotionDto(d,
            deciders.TryGetValue(d.StudentId, out var g) && g.Relationship != "self" ? g : null)).ToList();
    }
}
