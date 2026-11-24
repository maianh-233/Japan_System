using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Commissions.GetCommissions;

/// <summary>
/// TC-12 — bảng bậc + kết quả tháng của các Sale trong phạm vi (chi nhánh chính của Sale). Phạm vi <c>own</c> → chỉ kết quả của chính mình (E4).
/// Xếp hạng tính trên toàn bộ Sale (không đổi theo phạm vi xem).
/// </summary>
public sealed record GetCommissionsQuery(string? Month, Guid? BranchId) : IQuery<CommissionBoardDto>;

public sealed class GetCommissionsHandler(CommissionCalculator calculator, FinanceAccess access, ICurrentUser user)
    : IQueryHandler<GetCommissionsQuery, CommissionBoardDto>
{
    public async Task<Result<CommissionBoardDto>> Handle(GetCommissionsQuery query, CancellationToken ct)
    {
        var month = PayrollMonth.Parse(query.Month);
        if (month.IsFailure) return month.Error;
        var own = user.Permission(FinanceAccess.Commissions, PermissionAction.View)?.Scope == PermissionScope.Own;
        var allowed = access.Branches(FinanceAccess.Commissions, PermissionAction.View);
        var me = user.Access?.EmployeeId;

        var results = (await calculator.ResultsAsync(month.Value, ct))
            .Where(r => own
                ? r.EmployeeId == me
                : (allowed is null || allowed.Contains(r.BranchId)) && (query.BranchId is null || r.BranchId == query.BranchId))
            .ToList();
        var tiers = (await calculator.TiersAsync(ct)).Select(t => new CommissionTierDto(t.From.Amount, t.Percent)).ToList();
        return new CommissionBoardDto(month.Value, tiers, results);
    }
}
