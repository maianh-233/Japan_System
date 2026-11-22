using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.GetInstallmentPlans;

/// <summary>TC-02 — các chính sách đóng nhiều đợt (đang bật trước)</summary>
public sealed record GetInstallmentPlansQuery : IQuery<IReadOnlyList<InstallmentPlanDto>>;

public sealed class GetInstallmentPlansHandler(IFinanceDbContext db) : IQueryHandler<GetInstallmentPlansQuery, IReadOnlyList<InstallmentPlanDto>>
{
    public async Task<Result<IReadOnlyList<InstallmentPlanDto>>> Handle(GetInstallmentPlansQuery q, CancellationToken ct)
    {
        var rows = await db.InstallmentPlans.AsNoTracking().Select(p => new { p, v = EF.Property<uint>(p, "xmin") }).ToListAsync(ct);
        return rows.OrderByDescending(x => x.p.Active).ThenBy(x => x.p.Name).Select(x => x.p.ToDto(x.v)).ToList();
    }
}
