using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Portals.PromotionOffers;

/// <summary>DT-17 bước 3 — cổng HV / PH: lớp kỳ sau trung tâm gợi ý (đã chọn lớp đích, chưa chốt / từ chối)</summary>
public sealed record GetPromotionOffersQuery(Guid? StudentId) : IQuery<IReadOnlyList<PromotionOfferDto>>;

/// <summary>DT-17 bước 3 — HV / PH bấm "Quan tâm" lớp kỳ sau (giữ lần bấm đầu)</summary>
public sealed record MarkInterestCommand(Guid Id) : ICommand<PromotionOfferDto>;

public sealed class PromotionOffersHandler(IAcademicDbContext db, PortalOwner owner, PromotionReader reader, IClock clock)
    : IQueryHandler<GetPromotionOffersQuery, IReadOnlyList<PromotionOfferDto>>, ICommandHandler<MarkInterestCommand, PromotionOfferDto>
{
    public async Task<Result<IReadOnlyList<PromotionOfferDto>>> Handle(GetPromotionOffersQuery q, CancellationToken ct)
    {
        IReadOnlyList<Guid> students;
        if (q.StudentId is { } one)
        {
            var s = await owner.StudentAsync(one, ct);
            if (s.IsFailure) return s.Error;
            students = [s.Value];
        }
        else students = await owner.StudentsAsync(ct);

        var list = await db.PromotionCandidates.AsNoTracking()
            .Where(p => students.Contains(p.StudentId) && p.ToClassId != null
                && p.Status != PromotionStatus.Confirmed && p.Status != PromotionStatus.Declined)
            .ToListAsync(ct);
        return Result.Success(await reader.OffersAsync(list, ct));
    }

    public async Task<Result<PromotionOfferDto>> Handle(MarkInterestCommand cmd, CancellationToken ct)
    {
        var mine = await owner.StudentsAsync(ct);
        var p = await db.PromotionCandidates.FirstOrDefaultAsync(x => x.Id == new PromotionCandidateId(cmd.Id) && mine.Contains(x.StudentId), ct);
        if (p is null) return PromotionErrors.NotFound;
        if (p.MarkInterest(owner.IsParent ? InterestBy.Parent : InterestBy.Student, clock.UtcNow) is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        // ⏳ Báo Sale phụ trách khi HV / PH bấm Quan tâm — làm cùng module Thông báo
        return (await reader.OffersAsync([p], ct))[0];
    }
}
