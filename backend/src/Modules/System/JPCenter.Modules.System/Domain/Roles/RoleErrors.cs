using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Roles;

public static class RoleErrors
{
    public static readonly Error NameRequired = Error.Validation("system.role.name_required", "Chưa nhập tên vai trò");
    public static readonly Error SuperAdminLocked = Error.Conflict("system.role.super_admin_locked", "Không sửa quyền của Super Admin");
    public static readonly Error SystemRoleRename = Error.Conflict("system.role.system_role", "Không đổi tên được vai trò mặc định của hệ thống");
    public static readonly Error SystemRole = Error.Conflict("system.role.system_role", "Không xóa được vai trò mặc định của hệ thống");
}
