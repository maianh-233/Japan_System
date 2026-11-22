using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.SaveInstallmentPlan;

/// <summary>
/// TC-02 — tạo (<paramref name="Id"/> null) / sửa chính sách đóng đợt: tên, ≥ 2 đợt mỗi đợt &gt; 0% (E2), tổng 100% (E1), học phí áp dụng từ, phụ thu 0–50%.
/// <paramref name="Active"/> null = giữ nguyên (tạo mới: bật).
/// </summary>
public sealed record SaveInstallmentPlanCommand(Guid? Id, string? Name, IReadOnlyList<InstallmentPart> Parts, decimal MinFee, decimal Surcharge,
    bool? Active, uint? RowVersion) : ICommand<InstallmentPlanDto>;

public sealed class SaveInstallmentPlanHandler(IFinanceDbContext db) : ICommandHandler<SaveInstallmentPlanCommand, InstallmentPlanDto>
{
    public static readonly Error NotFound = Error.NotFound("finance.installment.not_found", "Không tìm thấy chính sách đóng đợt");

    public async Task<Result<InstallmentPlanDto>> Handle(SaveInstallmentPlanCommand cmd, CancellationToken ct)
    {
        var minFee = Money.Create(cmd.MinFee);
        if (minFee.IsFailure) return minFee.Error;
        InstallmentPlan plan;
        if (cmd.Id is { } id)
        {
            var found = await db.InstallmentPlans.FirstOrDefaultAsync(p => p.Id == new InstallmentPlanId(id), ct);
            if (found is null) return NotFound;
            if (cmd.RowVersion is { } v) ((DbContext)db).Entry(found).Property("xmin").OriginalValue = v;
            if (found.Update(cmd.Name, cmd.Parts, minFee.Value, cmd.Surcharge) is { IsFailure: true } f) return f.Error;
            plan = found;
        }
        else
        {
            var created = InstallmentPlan.Create(cmd.Name, cmd.Parts, minFee.Value, cmd.Surcharge);
            if (created.IsFailure) return created.Error;
            db.InstallmentPlans.Add(plan = created.Value);
        }
        if (cmd.Active is { } active) plan.SetActive(active);
        await db.SaveChangesAsync(ct);
        return plan.ToDto(((DbContext)db).Entry(plan).Property<uint>("xmin").CurrentValue);
    }
}
