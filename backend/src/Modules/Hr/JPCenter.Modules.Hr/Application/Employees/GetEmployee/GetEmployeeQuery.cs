using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.GetEmployee;

/// <summary>NS-05 — chi tiết hồ sơ (kèm lịch sử hợp đồng). Ngoài phạm vi / khác nhóm trang → 404.</summary>
public sealed record GetEmployeeQuery(EmployeeGroup Scope, Guid Id) : IQuery<EmployeeDto>;

public sealed class GetEmployeeHandler(EmployeeAccess access) : IQueryHandler<GetEmployeeQuery, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> Handle(GetEmployeeQuery q, CancellationToken ct)
    {
        var all = await access.LoadAllAsync(ct);
        var hit = access.InScope(all, q.Scope).FirstOrDefault(x => x.Employee.Id.Value == q.Id);
        if (hit.Employee is null) return EmployeeErrors.NotFound;
        return access.ToDto(hit.Employee, hit.Version, EmployeeAccess.Names(all), access.CanSeeSalary(q.Scope), withHistory: true);
    }
}
