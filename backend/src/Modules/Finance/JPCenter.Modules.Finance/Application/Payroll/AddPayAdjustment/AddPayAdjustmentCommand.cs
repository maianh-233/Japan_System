using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Payroll.AddPayAdjustment;

/// <summary>
/// TC-13 bước 3 / TC-14 — thưởng (+) / phạt (−) / phụ cấp tháng cho 1 người. Số tiền = 0 hoặc lẻ → <c>finance.pay_adjust.invalid_amount</c>,
/// thiếu lý do → <c>reason_required</c> (E1); tháng đã chốt ở chi nhánh của người đó → <c>finance.payroll.locked</c> (E2).
/// </summary>
public sealed record AddPayAdjustmentCommand(string? Month, Guid EmployeeId, decimal Amount, string? Reason) : ICommand<PayAdjustDto>;

public sealed class AddPayAdjustmentValidator : AbstractValidator<AddPayAdjustmentCommand>
{
    public AddPayAdjustmentValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Amount).InclusiveBetween(-1_000_000_000, 1_000_000_000);
    }
}

public sealed class AddPayAdjustmentHandler(IFinanceDbContext db, PayrollGuard guard, FinanceAccess access, PeopleNames names)
    : ICommandHandler<AddPayAdjustmentCommand, PayAdjustDto>
{
    public async Task<Result<PayAdjustDto>> Handle(AddPayAdjustmentCommand cmd, CancellationToken ct)
    {
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        var month = PayrollMonth.Parse(cmd.Month);
        if (month.IsFailure) return month.Error;
        var created = PayAdjustment.Create(month.Value, cmd.EmployeeId, cmd.Amount, cmd.Reason ?? "", actor);
        if (created.IsFailure) return created.Error;
        var allowed = await guard.CanAdjustAsync(cmd.EmployeeId, month.Value, ct);
        if (allowed.IsFailure) return allowed.Error;

        var a = created.Value;
        db.PayAdjustments.Add(a);
        await db.SaveChangesAsync(ct);
        var by = (await names.OfAsync([actor], ct)).GetValueOrDefault(actor, "—");
        return new PayAdjustDto(a.Id.Value, a.Month, a.EmployeeId, a.Amount, a.Reason, by);
    }
}
