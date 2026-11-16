namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>Trạng thái đơn</summary>
public enum RequestStatus
{
    /// <summary>Chờ duyệt</summary>
    Pending,
    /// <summary>Đã duyệt</summary>
    Approved,
    /// <summary>Từ chối</summary>
    Rejected,
}
