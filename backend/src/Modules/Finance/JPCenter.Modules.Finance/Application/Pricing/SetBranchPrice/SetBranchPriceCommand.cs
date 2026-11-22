using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.SetBranchPrice;

/// <summary>
/// TC-01 — đặt giá riêng của khóa ở chi nhánh (giá ≤ 0 → E1); <paramref name="Fee"/> null = xóa giá riêng, về giá niêm yết.
/// Chỉ áp cho lớp mở mới — hóa đơn cũ giữ nguyên (E2). Trả null khi đã xóa.
/// </summary>
public sealed record SetBranchPriceCommand(Guid CourseId, Guid BranchId, decimal? Fee) : ICommand<BranchPriceDto?>;

public sealed class SetBranchPriceHandler(IFinanceDbContext db, FinanceAccess access) : ICommandHandler<SetBranchPriceCommand, BranchPriceDto?>
{
    public async Task<Result<BranchPriceDto?>> Handle(SetBranchPriceCommand cmd, CancellationToken ct)
    {
        if (!access.InBranch(FinanceAccess.Tuition, PermissionAction.Update, cmd.BranchId)) return FinanceAccess.BranchNotAllowed;
        var branch = new BranchId(cmd.BranchId);
        var price = await db.BranchPrices.FirstOrDefaultAsync(p => p.CourseId == cmd.CourseId && p.BranchId == branch, ct);
        if (cmd.Fee is not { } fee)
        {
            if (price is not null)
            {
                db.BranchPrices.Remove(price);
                await db.SaveChangesAsync(ct);
            }
            return Result.Success<BranchPriceDto?>(null);
        }

        var money = Money.Create(fee);
        if (money.IsFailure || fee <= 0) return PricingErrors.InvalidFee;
        if (price is null)
        {
            var created = BranchPrice.Create(cmd.CourseId, branch, money.Value);
            if (created.IsFailure) return created.Error;
            db.BranchPrices.Add(price = created.Value);
        }
        else if (price.ChangeFee(money.Value) is { IsFailure: true } f) return f.Error;
        await db.SaveChangesAsync(ct);
        return new BranchPriceDto(price.CourseId, price.BranchId.Value, price.Fee.Amount);
    }
}
