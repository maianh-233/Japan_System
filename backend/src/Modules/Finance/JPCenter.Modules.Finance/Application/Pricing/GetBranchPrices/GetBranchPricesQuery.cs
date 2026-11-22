using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.GetBranchPrices;

/// <summary>TC-01 — ma trận giá riêng khóa × chi nhánh (ô không có = giá niêm yết của khóa). Xem được mọi chi nhánh để so sánh.</summary>
public sealed record GetBranchPricesQuery : IQuery<IReadOnlyList<BranchPriceDto>>;

public sealed class GetBranchPricesHandler(IFinanceDbContext db) : IQueryHandler<GetBranchPricesQuery, IReadOnlyList<BranchPriceDto>>
{
    public async Task<Result<IReadOnlyList<BranchPriceDto>>> Handle(GetBranchPricesQuery q, CancellationToken ct) =>
        await db.BranchPrices.AsNoTracking().Select(p => new BranchPriceDto(p.CourseId, p.BranchId.Value, p.Fee.Amount)).ToListAsync(ct);
}
