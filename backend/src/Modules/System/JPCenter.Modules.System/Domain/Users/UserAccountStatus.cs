namespace JPCenter.Modules.System.Domain.Users;

/// <summary>Trạng thái tài khoản nhân viên</summary>
public enum UserAccountStatus
{
    /// <summary>Đã mời, chưa kích hoạt</summary>
    Invited,
    /// <summary>Đang hoạt động</summary>
    Active,
    /// <summary>Bị khóa</summary>
    Locked,
}
