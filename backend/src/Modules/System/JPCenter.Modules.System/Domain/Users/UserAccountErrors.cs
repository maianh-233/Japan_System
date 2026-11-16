using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Users;

public static class UserAccountErrors
{
    public static readonly Error EmailRequired = Error.Validation("system.user.email_required", "Chưa nhập email đăng nhập");
    public static readonly Error AccountLocked = Error.Forbidden("auth.account_locked", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
    public static readonly Error AccountInvited = Error.Forbidden("auth.account_invited", "Tài khoản đang chờ kích hoạt.");
    public static readonly Error AlreadyLocked = Error.Conflict("system.user.already_locked", "Tài khoản đã bị khóa");
}
