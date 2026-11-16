using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>
/// Nhân viên / giáo viên / trợ giảng (1 bảng, phân biệt bằng Kind). Cây quản lý qua ManagerId (giao việc cấp dưới).
/// GV có hồ sơ giảng dạy (teacher_profiles) và chấm công THEO BUỔI ở Academic, không chấm công văn phòng.
/// Bảng: hr.employees (+ teacher_profiles)
/// </summary>
public sealed class Employee : AggregateRoot<EmployeeId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private Employee() { }

    /// <summary>Mã nhân viên, vd NV0012</summary>
    public string Code { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public Gender Gender { get; private set; }
    public DateOnly? Dob { get; private set; }
    public string Phone { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    /// <summary>Chi nhánh chính</summary>
    public BranchId BranchId { get; private set; }
    public EmployeeKind Kind { get; private set; }
    public Department Department { get; private set; }
    /// <summary>Chức danh, vd "Sale", "Giáo vụ"</summary>
    public string Position { get; private set; } = null!;
    /// <summary>Làm theo ca (Sale, lễ tân) → xếp ca hằng tuần; còn lại giờ hành chính</summary>
    public bool ShiftBased { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public DateOnly JoinedAt { get; private set; }
    public DateOnly? QuitAt { get; private set; }
    /// <summary>Quản lý trực tiếp</summary>
    public EmployeeId? ManagerId { get; private set; }
    /// <summary>Hợp đồng hiện tại (owned, cột contract_*)</summary>
    public EmploymentContract Contract { get; private set; } = null!;
    /// <summary>Ngày phép năm còn lại</summary>
    public decimal LeaveBalance { get; private set; }
    /// <summary>Đã có tài khoản đăng nhập (System.UserAccount)</summary>
    public bool HasAccount { get; private set; }
    /// <summary>Hồ sơ giảng dạy — chỉ Kind = Teacher / Assistant</summary>
    public TeacherProfile? Teacher { get; private set; }
    public string? Note { get; private set; }

    public static Result<Employee> Create(string code, string fullName, Gender gender, DateOnly? dob, string phone, string email, BranchId branchId,
        EmployeeKind kind, Department department, string position, bool shiftBased, DateOnly joinedAt, EmployeeId? managerId,
        EmploymentContract contract, decimal leaveBalance)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return Error.Validation("hr.employee.name_required", "Chưa nhập họ tên");
        if (string.IsNullOrWhiteSpace(email)) return Error.Validation("hr.employee.email_required", "Chưa nhập email");
        return new Employee
        {
            Id = EmployeeId.New(), Code = code, FullName = fullName.Trim(), Gender = gender, Dob = dob, Phone = phone, Email = email.Trim().ToLowerInvariant(),
            BranchId = branchId, Kind = kind, Department = department, Position = position, ShiftBased = shiftBased, JoinedAt = joinedAt,
            ManagerId = managerId, Contract = contract, LeaveBalance = leaveBalance,
            Status = contract.Type == ContractType.Probation ? EmployeeStatus.Probation : EmployeeStatus.Active,
        };
    }

    public Result SetManager(EmployeeId? managerId)
    {
        if (managerId == Id) return Error.Validation("hr.employee.self_manager", "Nhân viên không thể là quản lý của chính mình");
        ManagerId = managerId;
        return Result.Success();
    }

    public Result SetTeacherProfile(TeacherProfile profile)
    {
        if (Kind == EmployeeKind.Staff) return Error.Conflict("hr.employee.not_teacher", "Chỉ giáo viên / trợ giảng có hồ sơ giảng dạy");
        Teacher = profile;
        return Result.Success();
    }

    public void RenewContract(EmploymentContract contract)
    {
        Contract = contract;
        if (Status == EmployeeStatus.Probation && contract.Type != ContractType.Probation) Status = EmployeeStatus.Active;
    }

    public Result UseLeave(decimal days)
    {
        if (days > LeaveBalance) return Error.Conflict("hr.employee.leave_exceeded", "Không đủ ngày phép");
        LeaveBalance -= days;
        return Result.Success();
    }

    public void MarkHasAccount() => HasAccount = true;
    public void StartLongLeave() => Status = EmployeeStatus.Leave;
    public void ReturnFromLeave() => Status = EmployeeStatus.Active;
    public void Quit(DateOnly at) => (Status, QuitAt) = (EmployeeStatus.Quit, at);
}
