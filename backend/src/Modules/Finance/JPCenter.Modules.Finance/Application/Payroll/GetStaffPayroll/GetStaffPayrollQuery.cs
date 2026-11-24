using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Payroll.GetStaffPayroll;

/// <summary>TC-14 — bảng lương NV tháng: công từ bảng công, phụ cấp, hoa hồng, điều chỉnh, BHXH, thuế TNCN; tạm tính hoặc bản chốt</summary>
public sealed record GetStaffPayrollQuery(string? Month, Guid? BranchId) : IQuery<PayrollBoardDto<StaffPayDto>>;

public sealed class GetStaffPayrollHandler(PayrollBoards boards) : IQueryHandler<GetStaffPayrollQuery, PayrollBoardDto<StaffPayDto>>
{
    public async Task<Result<PayrollBoardDto<StaffPayDto>>> Handle(GetStaffPayrollQuery query, CancellationToken ct)
    {
        var month = PayrollMonth.Parse(query.Month);
        if (month.IsFailure) return month.Error;
        var branches = await boards.BranchesAsync(PayrollKind.Staff, PermissionAction.View, query.BranchId is { } b ? [b] : null, ct);
        if (branches.IsFailure) return branches.Error;
        return await boards.StaffAsync(month.Value, branches.Value, ct);
    }
}
