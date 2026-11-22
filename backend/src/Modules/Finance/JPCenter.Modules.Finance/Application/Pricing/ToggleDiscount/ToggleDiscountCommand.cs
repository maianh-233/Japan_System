using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.ToggleDiscount;

/// <summary>TC-03 — bật / tắt ưu đãi (tắt → không áp được khi ghi danh — E5; hóa đơn đã lập giữ nguyên)</summary>
public sealed record ToggleDiscountCommand(Guid Id, bool Active) : ICommand<DiscountDto>;

public sealed class ToggleDiscountHandler(IFinanceDbContext db, FinanceAccess access) : ICommandHandler<ToggleDiscountCommand, DiscountDto>
{
    public async Task<Result<DiscountDto>> Handle(ToggleDiscountCommand cmd, CancellationToken ct)
    {
        var d = await db.Discounts.FirstOrDefaultAsync(x => x.Id == new DiscountId(cmd.Id), ct);
        if (d is null) return PricingErrors.DiscountNotFound;
        if (access.Branches(FinanceAccess.Tuition, PermissionAction.Update) is { } allowed
            && (d.BranchIds.Count == 0 || d.BranchIds.Any(b => !allowed.Contains(b.Value))))
            return FinanceAccess.BranchNotAllowed;
        d.SetActive(cmd.Active);
        await db.SaveChangesAsync(ct);
        return d.ToDto(((DbContext)db).Entry(d).Property<uint>("xmin").CurrentValue);
    }
}
