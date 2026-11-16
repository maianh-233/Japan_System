namespace JPCenter.Modules.System.Domain.PortalAccounts;

/// <summary>Trạng thái tài khoản cổng</summary>
public enum PortalAccountStatus
{
    /// <summary>Đang hoạt động</summary>
    Active,
    /// <summary>Ngừng (học viên đã nghỉ)</summary>
    Inactive,
    /// <summary>Bị khóa</summary>
    Locked,
}
