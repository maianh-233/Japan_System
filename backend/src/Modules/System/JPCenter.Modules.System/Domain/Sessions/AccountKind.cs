namespace JPCenter.Modules.System.Domain.Sessions;

/// <summary>Loại tài khoản đăng nhập</summary>
public enum AccountKind
{
    /// <summary>Nhân viên / GV (user_accounts)</summary>
    Staff,
    /// <summary>Học viên / phụ huynh (portal_accounts)</summary>
    Portal,
}
