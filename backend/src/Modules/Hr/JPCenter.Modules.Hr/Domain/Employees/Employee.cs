using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>
/// Nhân viên / giáo viên / trợ giảng (1 bảng, phân biệt bằng Kind). Cây quản lý qua ManagerId (giao việc cấp dưới).
/// GV có hồ sơ giảng dạy (teacher_profiles) và chấm công THEO BUỔI ở Academic, không chấm công văn phòng.
/// Không xóa hồ sơ — nghỉ việc chuyển trạng thái <see cref="EmployeeStatus.Quit"/> (user story 04).
/// Bảng: hr.employees (+ teacher_profiles, employment_contracts)
/// </summary>
public sealed class Employee : AggregateRoot<EmployeeId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private readonly List<ContractHistoryEntry> _contractHistory = [];

    private Employee() { }

    /// <summary>Mã nhân viên, vd NV0012 — tự sinh theo loại, không đổi</summary>
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
    /// <summary>Ngày nghỉ việc. Có giá trị mà trạng thái chưa "đã nghỉ" = ĐÃ HẸN nghỉ (ngày ở tương lai — NS-07.E6)</summary>
    public DateOnly? QuitAt { get; private set; }
    /// <summary>Lý do nghỉ việc</summary>
    public string? QuitReason { get; private set; }
    /// <summary>Quản lý trực tiếp</summary>
    public EmployeeId? ManagerId { get; private set; }
    /// <summary>Hợp đồng hiện tại (owned, cột contract_*)</summary>
    public EmploymentContract Contract { get; private set; } = null!;
    /// <summary>Hợp đồng trước đây (mới nhất trước) — NS-06.E5</summary>
    public IReadOnlyList<ContractHistoryEntry> ContractHistory => _contractHistory;
    /// <summary>Ngày phép năm còn lại</summary>
    public decimal LeaveBalance { get; private set; }
    /// <summary>Đã có tài khoản đăng nhập (System.UserAccount) — cập nhật qua sự kiện StaffAccountCreated</summary>
    public bool HasAccount { get; private set; }
    /// <summary>Hồ sơ giảng dạy — chỉ Kind = Teacher / Assistant</summary>
    public TeacherProfile? Teacher { get; private set; }
    public string? Note { get; private set; }

    public bool IsTeaching => Kind is EmployeeKind.Teacher or EmployeeKind.Assistant;

    public bool HasQuit => Status == EmployeeStatus.Quit;

    /// <summary>Đã hẹn ngày nghỉ (chưa tới ngày)</summary>
    public bool QuitScheduled => !HasQuit && QuitAt is not null;

    /// <param name="status">Chỉ Thử việc / Đang làm (NS-03.E10); bỏ trống = theo loại hợp đồng</param>
    public static Result<Employee> Create(string code, string fullName, Gender gender, DateOnly? dob, string phone, string email, BranchId branchId,
        EmployeeKind kind, Department department, string position, bool shiftBased, DateOnly joinedAt, EmployeeId? managerId,
        EmploymentContract contract, decimal leaveBalance, EmployeeStatus? status = null)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return EmployeeErrors.NameRequired;
        if (string.IsNullOrWhiteSpace(email)) return EmployeeErrors.EmailRequired;
        if (status is EmployeeStatus.Quit or EmployeeStatus.Leave) return EmployeeErrors.StatusNotAllowed;
        if (ValidateContract(contract) is { IsFailure: true } bad) return bad.Error;
        var teaching = kind is EmployeeKind.Teacher or EmployeeKind.Assistant;
        return new Employee
        {
            Id = EmployeeId.New(), Code = code, FullName = fullName.Trim(), Gender = gender, Dob = dob, Phone = phone.Trim(),
            Email = email.Trim().ToLowerInvariant(), BranchId = branchId, Kind = kind,
            Department = teaching ? Department.Teaching : department, Position = position.Trim(), ShiftBased = !teaching && shiftBased,
            JoinedAt = joinedAt, ManagerId = managerId, Contract = contract, LeaveBalance = leaveBalance,
            Status = status ?? (contract.Type == ContractType.Probation ? EmployeeStatus.Probation : EmployeeStatus.Active),
        };
    }

    /// <summary>Hợp đồng có thời hạn phải có ngày kết thúc, sau ngày bắt đầu (NS-03.E4, E5)</summary>
    public static Result ValidateContract(EmploymentContract c)
    {
        if (c.Type != ContractType.Indefinite && c.End is null) return EmployeeErrors.ContractEndRequired;
        if (c.End is { } end && end <= c.Start) return EmployeeErrors.ContractDates;
        return Result.Success();
    }

    /// <summary>
    /// Sửa hồ sơ (NS-04). Loại, mã, số hợp đồng, ngày vào làm, phép, lịch rảnh giữ nguyên. GV / trợ giảng luôn phòng "Giảng dạy",
    /// không làm theo ca. Người đã nghỉ: dùng <see cref="UpdateContact"/>.
    /// </summary>
    public Result Update(string fullName, Gender gender, DateOnly? dob, string phone, string email, BranchId branchId, Department department,
        string position, bool shiftBased, string? note)
    {
        if (HasQuit) return EmployeeErrors.AlreadyQuit;
        if (string.IsNullOrWhiteSpace(fullName)) return EmployeeErrors.NameRequired;
        if (string.IsNullOrWhiteSpace(email)) return EmployeeErrors.EmailRequired;
        (FullName, Gender, Dob, Phone, Email, BranchId) = (fullName.Trim(), gender, dob, phone.Trim(), email.Trim().ToLowerInvariant(), branchId);
        (Department, Position, ShiftBased) = (IsTeaching ? Department.Teaching : department, position.Trim(), !IsTeaching && shiftBased);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }

    /// <summary>NS-04.E6 — người đã nghỉ chỉ sửa liên hệ + ghi chú</summary>
    public void UpdateContact(string phone, string? note)
    {
        Phone = phone.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Thử việc ↔ Đang làm; sang / từ Nghỉ dài hạn. Nghỉ việc có chức năng riêng (NS-04.E5).</summary>
    public Result ChangeStatus(EmployeeStatus status)
    {
        if (HasQuit) return EmployeeErrors.AlreadyQuit;
        if (status is EmployeeStatus.Quit) return EmployeeErrors.StatusNotAllowed;
        if (status == Status) return Result.Success();
        if (status is EmployeeStatus.Leave) return StartLongLeave();
        if (Status is EmployeeStatus.Leave)
        {
            if (ReturnFromLeave() is { IsFailure: true } r) return r;
            Status = status;
            return Result.Success();
        }
        Status = status;
        return Result.Success();
    }

    public Result SetManager(EmployeeId? managerId)
    {
        if (managerId == Id) return EmployeeErrors.SelfManager;
        ManagerId = managerId;
        return Result.Success();
    }

    public Result SetTeacherProfile(TeacherProfile profile)
    {
        if (!IsTeaching) return EmployeeErrors.NotTeacher;
        if (profile.Levels.Count == 0) return EmployeeErrors.LevelsRequired;
        // Giữ lịch rảnh hiện có khi sửa hồ sơ (lịch rảnh sửa ở NS-08)
        Teacher = Teacher is null ? profile : profile.WithAvailability(Teacher.Availability);
        return Result.Success();
    }

    /// <summary>NS-08 — lịch rảnh: thứ (1 = T2 … 7 = CN) → ca</summary>
    public Result SetAvailability(Dictionary<int, List<ShiftKey>> availability)
    {
        if (Teacher is null) return EmployeeErrors.NotTeacher;
        if (HasQuit) return EmployeeErrors.AlreadyQuit;
        Teacher = Teacher.WithAvailability(availability
            .Where(kv => kv.Key is >= 1 and <= 7)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Distinct().ToList()));
        return Result.Success();
    }

    /// <summary>NS-06 — gia hạn / ký hợp đồng mới. Hợp đồng cũ vào lịch sử. Đang thử việc + hợp đồng mới không phải thử việc → chính thức.</summary>
    public Result RenewContract(EmploymentContract contract, DateTimeOffset at, Guid? by)
    {
        if (HasQuit) return EmployeeErrors.AlreadyQuit;
        if (ValidateContract(contract) is { IsFailure: true } bad) return bad;
        _contractHistory.Insert(0, new ContractHistoryEntry(Contract, at, by));
        Contract = contract;
        if (Status == EmployeeStatus.Probation && contract.Type != ContractType.Probation) Status = EmployeeStatus.Active;
        return Result.Success();
    }

    public Result UseLeave(decimal days)
    {
        if (days > LeaveBalance) return EmployeeErrors.LeaveExceeded;
        LeaveBalance -= days;
        return Result.Success();
    }

    public void MarkHasAccount() => HasAccount = true;

    /// <summary>NS-10 — nghỉ dài hạn (thai sản, ốm dài ngày): không bị xếp ca / lớp, giữ tài khoản</summary>
    public Result StartLongLeave()
    {
        if (Status is not (EmployeeStatus.Active or EmployeeStatus.Probation)) return EmployeeErrors.InvalidStatus;
        Status = EmployeeStatus.Leave;
        return Result.Success();
    }

    public Result ReturnFromLeave()
    {
        if (Status != EmployeeStatus.Leave) return EmployeeErrors.InvalidStatus;
        Status = EmployeeStatus.Active;
        return Result.Success();
    }

    /// <summary>
    /// NS-07 — cho nghỉ việc. Ngày nghỉ ≤ hôm nay → nghỉ ngay (trả true); ở tương lai → chỉ hẹn ngày, job đổi trạng thái khi tới ngày (trả false).
    /// </summary>
    public Result<bool> Quit(DateOnly at, string reason, DateOnly today)
    {
        if (HasQuit) return EmployeeErrors.AlreadyQuit;
        if (string.IsNullOrWhiteSpace(reason)) return EmployeeErrors.QuitReasonRequired;
        (QuitAt, QuitReason) = (at, reason.Trim());
        if (at > today) return false;
        Status = EmployeeStatus.Quit;
        return true;
    }

    /// <summary>Job hằng ngày: tới ngày đã hẹn → nghỉ việc. Trả true nếu vừa chuyển.</summary>
    public bool ApplyScheduledQuit(DateOnly today)
    {
        if (!QuitScheduled || QuitAt > today) return false;
        Status = EmployeeStatus.Quit;
        return true;
    }

    /// <summary>Dữ liệu mẫu / nhập cũ: đã nghỉ từ trước</summary>
    public void MarkQuit(DateOnly at, string? reason) => (Status, QuitAt, QuitReason) = (EmployeeStatus.Quit, at, reason);
}
