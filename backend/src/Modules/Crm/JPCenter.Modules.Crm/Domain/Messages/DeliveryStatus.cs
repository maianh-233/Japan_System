namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Trạng thái gửi tin</summary>
public enum DeliveryStatus
{
    /// <summary>Chưa gửi được — chưa cấu hình tích hợp Zalo OA / SMS (CRM-17.E4), gửi khi có module Tích hợp</summary>
    Pending,
    /// <summary>Đã gửi</summary>
    Sent,
    /// <summary>Gửi lỗi</summary>
    Failed,
}
