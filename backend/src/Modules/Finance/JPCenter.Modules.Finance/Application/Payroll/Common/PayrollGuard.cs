using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.Common;

/// <summary>
/// Kiểm tra trước khi sửa điều chỉnh lương: người tồn tại, thuộc phạm vi chi nhánh của trang lương tương ứng (GV → lương GV, NV → lương NV),
/// tháng chưa chốt ở chi nhánh chính của người đó (TC-13.E2).
/// </summary>
public sealed class PayrollGuard(IFinanceDbContext db, IPayrollSource hr, FinanceAccess access)
{
    public static readonly Error EmployeeNotFound = Error.NotFound("finance.payroll.employee_not_found", "Không tìm thấy nhân viên");

    public async Task<Result<PayProfile>> CanAdjustAsync(Guid employeeId, string month, CancellationToken ct)
    {
        if (await hr.ProfileAsync(employeeId, ct) is not { } p) return EmployeeNotFound;
        var kind = p.IsTeacher ? PayrollKind.Teacher : PayrollKind.Staff;
        if (!access.InBranch(PayrollBoards.PageOf(kind), PermissionAction.Update, p.BranchId)) return FinanceAccess.BranchNotAllowed;
        var branch = new BranchId(p.BranchId);
        var locks = await db.PayrollLocks.AsNoTracking().Where(l => l.Kind == kind && l.Month == month && l.BranchId == branch).ToListAsync(ct);
        if (locks.Any(l => l.IsLocked)) return PayrollErrors.Locked;
        return p;
    }
}
