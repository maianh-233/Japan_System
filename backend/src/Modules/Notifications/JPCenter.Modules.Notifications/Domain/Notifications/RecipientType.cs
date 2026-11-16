namespace JPCenter.Modules.Notifications.Domain.Notifications;

/// <summary>Loại người nhận</summary>
public enum RecipientType
{
    /// <summary>Nhân viên / GV (System.UserAccount)</summary>
    Staff,
    /// <summary>Học viên / phụ huynh (System.PortalAccount)</summary>
    Portal,
}
