using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.PortalAccounts.Common;

public static class PortalAccountErrors
{
    public static readonly Error NotFound = Error.NotFound("system.portal_account.not_found", "Không tìm thấy tài khoản cổng.");
    /// <summary>AUTH-10.E1</summary>
    public static readonly Error AlreadyExists = Error.Conflict("system.portal_account.exists", "Người này đã có tài khoản — dùng Đặt lại mật khẩu / Khóa / Mở khóa.");
    public static readonly Error GuardianPermission = Error.Forbidden("system.permission.forbidden", "Bạn không có quyền cấp tài khoản cho phụ huynh.");

    /// <summary>AUTH-10.E2 — báo trùng + gợi ý hậu tố</summary>
    public static Error DuplicateUsername(string suggestion) =>
        Error.Conflict("system.portal_account.duplicate_username", $"Tên đăng nhập đã được dùng. Gợi ý: {suggestion}");
}
