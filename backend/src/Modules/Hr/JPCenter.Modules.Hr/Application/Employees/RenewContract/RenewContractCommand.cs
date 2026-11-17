using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.RenewContract;

/// <summary>
/// NS-06 — gia hạn / ký hợp đồng mới. Bắt đầu = ngày sau khi hợp đồng cũ kết thúc (hợp đồng cũ không thời hạn → hôm nay — E3).
/// Thử việc + hợp đồng mới không phải thử việc → chính thức. Hợp đồng cũ vào lịch sử (E5).
/// </summary>
/// <param name="Type">Bỏ trống = giữ loại cũ (thử việc → xác định thời hạn)</param>
/// <param name="End">Bắt buộc trừ "Không xác định thời hạn" (E2)</param>
public sealed record RenewContractCommand(EmployeeGroup Scope, Guid Id, ContractType? Type, DateOnly? End, decimal? BaseSalary, decimal? Allowance)
    : ICommand<EmployeeDto>;

public sealed class RenewContractValidator : AbstractValidator<RenewContractCommand>
{
    public RenewContractValidator()
    {
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0).When(x => x.BaseSalary is not null);
        RuleFor(x => x.Allowance).GreaterThanOrEqualTo(0).When(x => x.Allowance is not null);
    }
}

public sealed class RenewContractHandler(IHrDbContext db, EmployeeAccess access, ICurrentUser user, IAuditTrail audit, IClock clock)
    : ICommandHandler<RenewContractCommand, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> Handle(RenewContractCommand cmd, CancellationToken ct)
    {
        var e = await access.FindForUpdateAsync(cmd.Id, cmd.Scope, ct);
        if (e is null) return EmployeeErrors.NotFound;

        var old = e.Contract;
        var type = cmd.Type ?? (old.Type == ContractType.Probation ? ContractType.Fixed : old.Type);
        var start = old.End is { } end ? end.AddDays(1) : clock.Today;
        var no = EmployeeRules.ContractNo(e.Code, start.Year, e.ContractHistory.Count + 2);
        // Lương / phụ cấp bỏ trống = giữ như hợp đồng cũ
        var input = new ContractInput(type, start, cmd.End, cmd.BaseSalary ?? old.BaseSalary?.Amount, cmd.Allowance ?? old.Allowance?.Amount);
        var contract = EmployeeRules.Contract(input, no);
        if (contract.IsFailure) return contract.Error;
        if (e.RenewContract(contract.Value, clock.UtcNow, user.AccountId) is { IsFailure: true } failed) return failed.Error;

        await db.SaveChangesAsync(ct);
        var salaryChanged = old.BaseSalary != contract.Value.BaseSalary;
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Gia hạn hợp đồng", e.FullName,
            $"{old.No} → {contract.Value.No} · đến {(contract.Value.End is { } d ? d.ToString("dd/MM/yyyy") : "không thời hạn")}" +
            (salaryChanged ? " · đổi lương cơ bản" : ""), branchId: e.BranchId.Value);
        return await access.DetailAsync(e.Id, cmd.Scope, ct);
    }
}
