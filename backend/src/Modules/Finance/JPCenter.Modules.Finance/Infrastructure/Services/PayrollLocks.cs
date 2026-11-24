using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IPayrollLocks"/> — bảng lương GV tháng của chi nhánh có dòng chốt chưa mở khóa (TC-13.E2)</summary>
internal sealed class PayrollLocks(FinanceDbContext db) : IPayrollLocks
{
    public Task<bool> IsTeacherPayrollLockedAsync(Guid branchId, DateOnly date, CancellationToken ct)
    {
        var month = PayrollMonth.Of(date);
        var branch = new BranchId(branchId);
        return db.PayrollLocks.AsNoTracking()
            .AnyAsync(l => l.Kind == PayrollKind.Teacher && l.Month == month && l.BranchId == branch && l.UnlockedAt == null, ct);
    }
}
