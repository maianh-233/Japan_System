namespace JPCenter.Modules.Crm.Domain.Campaigns;

/// <summary>Trạng thái chiến dịch</summary>
public enum CampaignStatus
{
    /// <summary>Lên kế hoạch</summary>
    Planned,
    /// <summary>Đang chạy</summary>
    Running,
    /// <summary>Tạm dừng</summary>
    Paused,
    /// <summary>Kết thúc</summary>
    Ended,
}
