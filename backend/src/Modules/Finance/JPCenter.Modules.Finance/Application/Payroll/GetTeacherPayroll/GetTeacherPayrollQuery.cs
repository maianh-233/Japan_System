using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Payroll.GetTeacherPayroll;

/// <summary>TC-13 — bảng lương GV tháng (1 chi nhánh hoặc mọi chi nhánh trong phạm vi): tạm tính hoặc bản chốt, buổi chờ xác nhận, hạn chốt</summary>
public sealed record GetTeacherPayrollQuery(string? Month, Guid? BranchId) : IQuery<PayrollBoardDto<TeacherPayDto>>;

public sealed class GetTeacherPayrollHandler(PayrollBoards boards) : IQueryHandler<GetTeacherPayrollQuery, PayrollBoardDto<TeacherPayDto>>
{
    public async Task<Result<PayrollBoardDto<TeacherPayDto>>> Handle(GetTeacherPayrollQuery query, CancellationToken ct)
    {
        var month = PayrollMonth.Parse(query.Month);
        if (month.IsFailure) return month.Error;
        var branches = await boards.BranchesAsync(PayrollKind.Teacher, PermissionAction.View, query.BranchId is { } b ? [b] : null, ct);
        if (branches.IsFailure) return branches.Error;
        return await boards.TeacherAsync(month.Value, branches.Value, ct);
    }
}
