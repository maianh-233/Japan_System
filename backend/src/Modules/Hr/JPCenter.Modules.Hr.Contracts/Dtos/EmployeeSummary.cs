namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>Thông tin nhân viên module khác được đọc (đăng nhập, phân quyền, gán GV...).</summary>
/// <param name="Kind">"staff" · "teacher" · "assistant"</param>
/// <param name="Status">"probation" · "active" · "leave" · "quit"</param>
/// <param name="Department">"board" · "academic" · "sales" · "marketing" · "finance" · "reception" · "it" · "teaching"</param>
/// <param name="ManagerId">Quản lý trực tiếp (Hr.Employee) — Work giao việc cấp dưới (CV-04)</param>
/// <param name="ShiftBased">Làm theo ca — Work xếp ca, bàn giao ca (XC, BG)</param>
public sealed record EmployeeSummary(
    Guid Id,
    string Code,
    string FullName,
    string Email,
    string Kind,
    Guid BranchId,
    string Status,
    string Position,
    string Department,
    Guid? ManagerId = null,
    bool ShiftBased = false)
{
    public bool IsTeacher => Kind is "teacher" or "assistant";
    public bool HasQuit => Status == "quit";
    /// <summary>Đang làm việc (thử việc / chính thức) — không nghỉ dài hạn, không nghỉ việc</summary>
    public bool IsWorking => Status is "probation" or "active";
}
