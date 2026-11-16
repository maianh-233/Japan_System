namespace JPCenter.Modules.Academic.Domain.Promotions;

/// <summary>Tiến độ tư vấn lên lớp</summary>
public enum PromotionStatus
{
    /// <summary>Chờ tư vấn</summary>
    Pending,
    /// <summary>Đang tư vấn</summary>
    Consulting,
    /// <summary>Chưa liên hệ được</summary>
    Unreachable,
    /// <summary>Đồng ý (đã ghi danh)</summary>
    Confirmed,
    /// <summary>Từ chối</summary>
    Declined,
}
