using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Application.Common;

/// <summary>
/// Phạm vi chi nhánh của báo cáo (user story 17, quy tắc vàng 3). Kết quả <c>null</c> = mọi chi nhánh.
/// Phạm vi <c>branch</c> / <c>own</c> chọn chi nhánh ngoài phạm vi → coi như chọn chi nhánh của mình (không báo lỗi, không lộ số liệu).
/// </summary>
public sealed class ReportingAccess(ICurrentUser user)
{
    public const string Overview = "dashboard.overview";
    public const string Reports = "dashboard.reports";

    /// <summary>Chi nhánh xem được của trang (<paramref name="page"/>) theo chi nhánh đang chọn trên thanh trên cùng</summary>
    public IReadOnlyCollection<Guid>? Branches(string page, PermissionAction action, Guid? selected)
    {
        var allowed = user.AllowedBranches(page, action);
        if (allowed is null) return selected is { } s ? [s] : null;
        if (selected is { } b && allowed.Contains(b)) return [b];
        return allowed;
    }

    /// <summary>Thu hẹp <paramref name="branches"/> theo phạm vi của 1 quyền nghiệp vụ (Việc hôm nay — BC-02)</summary>
    public IReadOnlyCollection<Guid>? Narrow(IReadOnlyCollection<Guid>? branches, string page, PermissionAction action)
    {
        var allowed = user.AllowedBranches(page, action);
        if (allowed is null) return branches;
        return branches is null ? allowed : branches.Where(allowed.Contains).ToList();
    }

    public bool Can(string page, PermissionAction action) => user.Can(page, action);

    /// <summary>Quyền nghiệp vụ chỉ ở phạm vi <c>own</c> (vd Sale chỉ thấy lead của mình — BC-02.E1)</summary>
    public bool OwnOnly(string page, PermissionAction action) => user.Permission(page, action)?.Scope == PermissionScope.Own;

    /// <summary>Nhân viên đang đăng nhập (Hr.Employee)</summary>
    public Guid? EmployeeId => user.Access?.EmployeeId;

    /// <summary>Có chi nhánh nào để xem (rỗng = không)</summary>
    public static bool Any(IReadOnlyCollection<Guid>? branches) => branches is null || branches.Count > 0;
}
