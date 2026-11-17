using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Application.Employees.Common;

/// <summary>
/// 2 trang dùng chung 1 bảng nhân sự: Nhân viên (<c>hr.employees</c>, kind = staff) và Giáo viên & trợ giảng (<c>hr.teachers</c>).
/// API tách 2 đường dẫn để kiểm quyền ngay ở endpoint; hồ sơ khác nhóm → 404.
/// </summary>
public enum EmployeeGroup
{
    Staff,
    Teaching,
}

public static class EmployeeGroups
{
    /// <summary>Mã trang quyền</summary>
    public static string Page(this EmployeeGroup g) => g == EmployeeGroup.Staff ? "hr.employees" : "hr.teachers";

    /// <summary>Trang lương tương ứng (xem lương — NS-05.E3)</summary>
    public static string PayrollPage(this EmployeeGroup g) => g == EmployeeGroup.Staff ? "finance.staff-payroll" : "finance.teacher-payroll";

    public static bool Contains(this EmployeeGroup g, EmployeeKind kind) =>
        g == EmployeeGroup.Staff ? kind == EmployeeKind.Staff : kind is EmployeeKind.Teacher or EmployeeKind.Assistant;

    public static EmployeeGroup Of(EmployeeKind kind) => kind == EmployeeKind.Staff ? EmployeeGroup.Staff : EmployeeGroup.Teaching;

    /// <summary>Đường dẫn frontend mở hồ sơ</summary>
    public static string ProfilePath(EmployeeKind kind, Guid id) =>
        $"/admin/hr/{(kind == EmployeeKind.Staff ? "employees" : "teachers")}?open={id}";
}
