namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>Thông tin nhân viên module khác được đọc (đăng nhập, phân quyền, gán GV...).</summary>
/// <param name="Kind">"staff" · "teacher" · "assistant"</param>
/// <param name="Status">"probation" · "active" · "leave" · "quit"</param>
public sealed record EmployeeSummary(
    Guid Id,
    string Code,
    string FullName,
    string Email,
    string Kind,
    Guid BranchId,
    string Status,
    string Position,
    string Department)
{
    public bool IsTeacher => Kind is "teacher" or "assistant";
    public bool HasQuit => Status == "quit";
}
