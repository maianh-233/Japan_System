using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Permissions.Common;

/// <summary>Lỗi nhóm vai trò & phân quyền (user story 02) — mã trùng tài liệu.</summary>
public static class PermissionErrors
{
    public static readonly Error SelfEdit = Error.Forbidden("system.permission.self_edit", "Đây là tài khoản của bạn — không tự sửa quyền của mình được.");
    public static readonly Error ExceedsGranter = Error.Forbidden("system.permission.exceeds_granter",
        "Bạn chỉ cấp được quyền mình đang có, với phạm vi không rộng hơn của mình.");
    public static readonly Error BranchNotAllowed = Error.Forbidden("system.permission.branch_not_allowed",
        "Bạn chỉ gán được ở các chi nhánh mình được phân công, không chọn được \"mọi chi nhánh\".");
    public static readonly Error LastSuperAdmin = Error.Conflict("system.permission.last_super_admin",
        "Hệ thống phải còn ít nhất 1 Super Admin đang hoạt động.");
    public static readonly Error InvalidUntil = Error.Validation("system.permission.invalid_until", "Hạn quyền lẻ phải ở tương lai.");
    public static readonly Error UnknownPermission = Error.Validation("system.permission.unknown", "Mã quyền không tồn tại.");
    public static readonly Error UserNotFound = Error.NotFound("system.user.not_found", "Không tìm thấy người dùng.");
    public static readonly Error RoleNotFound = Error.NotFound("system.role.not_found", "Không tìm thấy vai trò.");
    public static readonly Error DuplicateRoleName = Error.Conflict("system.role.duplicate_name", "Tên vai trò đã tồn tại.");
    public static readonly Error OverrideNotFound = Error.NotFound("system.permission.override_not_found", "Không tìm thấy quyền lẻ.");
    public static readonly Error EmployeeNotFound = Error.NotFound("hr.employee.not_found", "Không tìm thấy nhân viên.");
    public static readonly Error EmployeeHasAccount = Error.Conflict("system.user.exists", "Nhân viên này đã có tài khoản.");
    public static readonly Error EmployeeQuit = Error.Conflict("system.user.employee_quit", "Nhân viên đã nghỉ việc — không cấp tài khoản.");
    /// <summary>PQ-08.E2</summary>
    public static readonly Error EmployeeNoEmail = Error.Validation("system.user.no_email", "Nhân viên chưa có email — cập nhật hồ sơ Nhân sự trước.");
    /// <summary>PQ-08.E3</summary>
    public static readonly Error DuplicateEmail = Error.Conflict("system.user.duplicate_email", "Email đã dùng cho tài khoản khác.");
    public static readonly Error NotLocked = Error.Conflict("system.user.not_locked", "Tài khoản không bị khóa.");
    public static readonly Error NotInvited = Error.Conflict("system.user.not_invited", "Tài khoản đã kích hoạt — không cần gửi lại lời mời.");

    /// <summary>PQ-04.E2 — kèm danh sách người đang dùng</summary>
    public static Error RoleInUse(IEnumerable<string> users) =>
        Error.Conflict("system.role.in_use", $"Không xóa được vai trò đang dùng: {string.Join(", ", users)}");
}
