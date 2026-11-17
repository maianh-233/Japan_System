using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>Lỗi nhóm nhân sự (user story 04) — mã trùng tài liệu.</summary>
public static class EmployeeErrors
{
    public static readonly Error NameRequired = Error.Validation("hr.employee.name_required", "Chưa nhập họ tên");
    public static readonly Error EmailRequired = Error.Validation("hr.employee.email_required", "Chưa nhập email");
    public static readonly Error NotFound = Error.NotFound("hr.employee.not_found", "Không tìm thấy hồ sơ nhân sự");
    public static readonly Error SelfManager = Error.Validation("hr.employee.self_manager", "Nhân viên không thể là quản lý của chính mình");
    /// <summary>NS-04.E2</summary>
    public static readonly Error ManagerCycle = Error.Validation("hr.employee.manager_cycle", "Không chọn được — người này đang là cấp dưới (trực tiếp hoặc gián tiếp) của nhân viên đang sửa");
    /// <summary>NS-03.E9</summary>
    public static readonly Error ManagerInvalid = Error.Validation("hr.employee.manager_invalid", "Quản lý trực tiếp phải là nhân viên đang làm việc");
    public static readonly Error NotTeacher = Error.Conflict("hr.employee.not_teacher", "Chỉ giáo viên / trợ giảng có hồ sơ giảng dạy");
    public static readonly Error LevelsRequired = Error.Validation("hr.employee.levels_required", "Chọn ít nhất 1 cấp độ dạy");
    public static readonly Error LeaveExceeded = Error.Conflict("hr.employee.leave_exceeded", "Không đủ ngày phép");
    /// <summary>NS-03.E4</summary>
    public static readonly Error ContractEndRequired = Error.Validation("hr.employee.contract_end_required", "Hợp đồng có thời hạn cần ngày kết thúc");
    /// <summary>NS-03.E5, NS-06.E1</summary>
    public static readonly Error ContractDates = Error.Validation("hr.employee.contract_dates", "Ngày kết thúc hợp đồng phải sau ngày bắt đầu");
    /// <summary>NS-04.E6, NS-06.E4, NS-07.E1</summary>
    public static readonly Error AlreadyQuit = Error.Conflict("hr.employee.already_quit", "Nhân sự đã nghỉ việc");
    /// <summary>NS-03.E10, NS-04.E5 — nghỉ việc chỉ qua chức năng "Cho nghỉ việc"</summary>
    public static readonly Error StatusNotAllowed = Error.Validation("hr.employee.status_not_allowed", "Trạng thái này đổi bằng chức năng riêng (Cho nghỉ việc)");
    public static readonly Error InvalidStatus = Error.Conflict("hr.employee.invalid_status", "Không chuyển được trạng thái hiện tại");
    /// <summary>NS-03.E7</summary>
    public static readonly Error BranchInactive = Error.Validation("hr.employee.branch_inactive", "Chi nhánh đã đóng — chọn chi nhánh khác");
    /// <summary>NS-03.E3</summary>
    public static readonly Error DuplicateEmail = Error.Conflict("hr.employee.duplicate_email", "Email đã dùng cho nhân sự khác");
    /// <summary>NS-07.E7</summary>
    public static readonly Error LastSuperAdmin = Error.Conflict("hr.employee.last_super_admin",
        "Đây là Super Admin cuối cùng — cấp Super Admin cho người khác trước khi cho nghỉ việc");
    public static readonly Error QuitReasonRequired = Error.Validation("hr.employee.quit_reason_required", "Nhập lý do nghỉ việc");
    public static readonly Error QuitBeforeJoin = Error.Validation("hr.employee.quit_before_join", "Ngày nghỉ việc không được trước ngày vào làm");
    public static readonly Error NoScheduledQuit = Error.Conflict("hr.employee.no_scheduled_quit", "Hồ sơ này không có lịch hẹn nghỉ việc");
    public static readonly Error InvalidDob = Error.Validation("hr.employee.invalid_dob", "Ngày sinh không hợp lệ (phải đủ 15 tuổi)");
}
