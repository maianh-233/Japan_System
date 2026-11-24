using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.GetMyPayroll;

/// <summary>
/// TC-13 bước 6 — cổng GV "Chấm công": buổi đã dạy + lương tạm tính của CHÍNH MÌNH trong tháng (gộp mọi chi nhánh).
/// <c>locked</c> = mọi chi nhánh liên quan (chi nhánh chính + chi nhánh có buổi) đã chốt; <c>paid</c> = đã chi lương. Không trả lương người khác.
/// </summary>
public sealed record GetMyPayrollQuery(string? Month) : IQuery<MyPayrollDto>;

/// <param name="Slip">Phiếu lương tạm tính (null = không có dữ liệu tháng này)</param>
public sealed record MyPayrollDto(string Month, TeacherPayDto? Slip, bool Locked, bool Paid);

public sealed class GetMyPayrollHandler(IFinanceDbContext db, PayrollCalculator calculator, ICurrentUser user) : IQueryHandler<GetMyPayrollQuery, MyPayrollDto>
{
    public static readonly Error NotTeacher = Error.Forbidden("finance.payroll.not_teacher", "Tài khoản không gắn hồ sơ giáo viên");

    public async Task<Result<MyPayrollDto>> Handle(GetMyPayrollQuery query, CancellationToken ct)
    {
        var month = PayrollMonth.Parse(query.Month);
        if (month.IsFailure) return month.Error;
        if (user.Access?.EmployeeId is not { } me) return NotTeacher;

        var slip = (await calculator.TeachersAsync(month.Value, null, me, ct)).FirstOrDefault();
        if (slip is null) return new MyPayrollDto(month.Value, null, false, false);

        var branches = slip.Items.Select(i => i.BranchId).Append(slip.BranchId).Distinct().ToList();
        var locks = (await db.PayrollLocks.AsNoTracking().Where(l => l.Kind == PayrollKind.Teacher && l.Month == month.Value).ToListAsync(ct))
            .Where(l => l.IsLocked).ToDictionary(l => l.BranchId.Value);
        var locked = branches.All(locks.ContainsKey);
        return new MyPayrollDto(month.Value, slip, locked, locked && branches.All(b => locks[b].IsPaid));
    }
}
