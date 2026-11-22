using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.GetDiscounts;

/// <summary>TC-03 — danh sách ưu đãi (đang bật trước, mới hết hạn sau)</summary>
public sealed record GetDiscountsQuery : IQuery<IReadOnlyList<DiscountDto>>;

public sealed class GetDiscountsHandler(IFinanceDbContext db) : IQueryHandler<GetDiscountsQuery, IReadOnlyList<DiscountDto>>
{
    public async Task<Result<IReadOnlyList<DiscountDto>>> Handle(GetDiscountsQuery q, CancellationToken ct)
    {
        var rows = await db.Discounts.AsNoTracking().Select(d => new { d, v = EF.Property<uint>(d, "xmin") }).ToListAsync(ct);
        return rows.OrderByDescending(x => x.d.Active).ThenByDescending(x => x.d.To).Select(x => x.d.ToDto(x.v)).ToList();
    }
}
