using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.ExpirePromotionHolds;

/// <summary>DT-17.E3 — job hằng ngày: chỗ giữ quá hạn mà chưa chốt → trả chỗ (CRM-19.E3). Trả số chỗ đã trả.</summary>
public sealed record ExpirePromotionHoldsCommand : ICommand<int>;

public sealed class ExpirePromotionHoldsHandler(IAcademicDbContext db, IClock clock) : ICommandHandler<ExpirePromotionHoldsCommand, int>
{
    public async Task<Result<int>> Handle(ExpirePromotionHoldsCommand cmd, CancellationToken ct)
    {
        var today = clock.Today;
        var held = await db.PromotionCandidates.IgnoreQueryFilters().Where(p => p.SeatHeld && p.HoldUntil < today).ToListAsync(ct);
        var released = held.Count(p => p.ExpireHold(today));
        if (released > 0) await db.SaveChangesAsync(ct);
        return released;
    }
}
