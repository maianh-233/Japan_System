namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Trạng thái lượt làm test</summary>
public enum AttemptStatus
{
    /// <summary>Đã gửi link</summary>
    Sent,
    /// <summary>Đang làm</summary>
    InProgress,
    /// <summary>Đã nộp</summary>
    Done,
    /// <summary>Hết hạn</summary>
    Expired,
}
