namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Trạng thái đơn</summary>
public enum HrRequestStatus
{
    /// <summary>Chờ duyệt</summary>
    Pending,
    /// <summary>Đã duyệt</summary>
    Approved,
    /// <summary>Từ chối</summary>
    Rejected,
    /// <summary>Người gửi hủy</summary>
    Cancelled,
}
