using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Users.GetEmployeesWithoutAccount;

/// <summary>PQ-08 bước 1 — "N nhân viên chưa có tài khoản" (chưa nghỉ việc, chưa có user_accounts).</summary>
public sealed record GetEmployeesWithoutAccountQuery : IQuery<IReadOnlyList<EmployeeSummary>>;

public sealed class GetEmployeesWithoutAccountHandler(ISystemDbContext db, IEmployeeDirectory employees)
    : IQueryHandler<GetEmployeesWithoutAccountQuery, IReadOnlyList<EmployeeSummary>>
{
    public async Task<Result<IReadOnlyList<EmployeeSummary>>> Handle(GetEmployeesWithoutAccountQuery query, CancellationToken ct)
    {
        // Tính ở System (nguồn sự thật về tài khoản) thay vì cờ HasAccount bên Hr
        var withAccount = (await db.UserAccounts.AsNoTracking().Select(a => a.EmployeeId).ToListAsync(ct)).ToHashSet();
        var list = (await employees.ListActiveAsync(ct)).Where(e => !withAccount.Contains(e.Id)).ToList();
        return Result.Success<IReadOnlyList<EmployeeSummary>>(list);
    }
}
