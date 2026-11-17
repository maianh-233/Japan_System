using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.MyProfile;

/// <summary>
/// NS-11 — hồ sơ của chính người đăng nhập (không cần mã quyền — PQ-10.E5). Không trả lương / phụ cấp / đơn giá (E4):
/// <c>salaryHidden = true</c>. Tài khoản không gắn hồ sơ nhân sự → 404 (E1).
/// </summary>
public sealed record GetMyProfileQuery : IQuery<EmployeeDto>;

public sealed class GetMyProfileHandler(EmployeeAccess access, ICurrentUser user) : IQueryHandler<GetMyProfileQuery, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> Handle(GetMyProfileQuery query, CancellationToken ct)
    {
        if (user.Access?.EmployeeId is not { } me) return EmployeeErrors.NotFound;
        var all = await access.LoadAllAsync(ct);
        var self = all.FirstOrDefault(x => x.Employee.Id.Value == me);
        if (self.Employee is null) return EmployeeErrors.NotFound;
        return access.ToDto(self.Employee, self.Version, EmployeeAccess.Names(all), canSeeSalary: false);
    }
}
