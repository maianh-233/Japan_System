using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Commissions.SaveCommissionTiers;

/// <summary>
/// TC-12 bước 1 — thay TOÀN BỘ bảng bậc hoa hồng: bắt buộc mốc 0đ (<c>zero_tier_required</c>), không trùng mốc (<c>duplicate_tier</c>),
/// % trong 0 → 100 (<c>invalid_percent</c>). Mốc âm / lẻ → lỗi số tiền.
/// </summary>
public sealed record SaveCommissionTiersCommand(IReadOnlyList<CommissionTierDto> Tiers) : ICommand<IReadOnlyList<CommissionTierDto>>;

public sealed class SaveCommissionTiersValidator : AbstractValidator<SaveCommissionTiersCommand>
{
    public SaveCommissionTiersValidator() =>
        RuleFor(x => x.Tiers).NotEmpty().WithMessage("Cần có bậc bắt đầu từ 0đ").Must(t => t is null || t.Count <= 20).WithMessage("Tối đa 20 bậc");
}

public sealed class SaveCommissionTiersHandler(IFinanceDbContext db) : ICommandHandler<SaveCommissionTiersCommand, IReadOnlyList<CommissionTierDto>>
{
    public async Task<Result<IReadOnlyList<CommissionTierDto>>> Handle(SaveCommissionTiersCommand cmd, CancellationToken ct)
    {
        var input = new List<(Money From, decimal Percent)>();
        foreach (var t in cmd.Tiers)
        {
            var from = Money.Create(t.From);
            if (from.IsFailure) return from.Error;
            input.Add((from.Value, t.Percent));
        }
        var built = CommissionTable.Build(input);
        if (built.IsFailure) return built.Error;

        db.CommissionTiers.RemoveRange(await db.CommissionTiers.ToListAsync(ct));
        db.CommissionTiers.AddRange(built.Value);
        await db.SaveChangesAsync(ct);
        return built.Value.Select(t => new CommissionTierDto(t.From.Amount, t.Percent)).ToList();
    }
}
