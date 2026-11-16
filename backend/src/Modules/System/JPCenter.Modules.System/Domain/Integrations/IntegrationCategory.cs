namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Nhóm tích hợp</summary>
public enum IntegrationCategory
{
    /// <summary>Gửi thông báo (Zalo OA, email, SMS)</summary>
    Notify,
    /// <summary>Thanh toán (ngân hàng / VietQR)</summary>
    Payment,
    /// <summary>Học online (Zoom / Google Meet)</summary>
    Meeting,
    /// <summary>Nguồn lead (Facebook, form website)</summary>
    Lead,
}
