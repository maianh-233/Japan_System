using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Common;

/// <summary>
/// Phạm vi quyền (PQ-10) cho các trang Hr ngoài hồ sơ: chấm công, đơn từ, tuyển dụng, onboarding.
/// <c>all</c> mọi chi nhánh · <c>branch</c> chi nhánh được gán · <c>own</c> chính mình + cấp dưới (theo người) / chi nhánh của mình (theo chi nhánh).
/// </summary>
public sealed class HrScope(ICurrentUser user)
{
    public const string Timesheets = "hr.timesheets";
    public const string Requests = "hr.requests";
    public const string Recruitment = "hr.recruitment";
    public const string Onboarding = "hr.onboarding";

    public bool Can(string page, PermissionAction action) => user.Can(page, action);

    /// <summary>Chi nhánh <paramref name="branchId"/> trong phạm vi quyền</summary>
    public bool InBranch(string page, PermissionAction action, Guid branchId) => user.Can(page, action) && user.InBranch(page, action, branchId);

    /// <summary>Nhân sự trong phạm vi quyền (theo chi nhánh chính; phạm vi own = mình + cấp dưới)</summary>
    public IEnumerable<Employee> Employees(IReadOnlyList<Employee> all, string page, PermissionAction action)
    {
        var perm = user.Permission(page, action);
        if (perm is null) return [];
        if (perm.Scope == PermissionScope.Own)
        {
            if (user.Access?.EmployeeId is not { } me) return [];
            var visible = EmployeeAccess.Subordinates(all, new EmployeeId(me));
            visible.Add(new EmployeeId(me));
            return all.Where(e => visible.Contains(e.Id));
        }
        var branches = user.AllowedBranches(page, action);
        return branches is null ? all : all.Where(e => branches.Contains(e.BranchId.Value));
    }
}
