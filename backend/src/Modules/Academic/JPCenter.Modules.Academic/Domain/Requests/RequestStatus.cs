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
    /// <summary>Người gửi thu hồi khi còn chờ duyệt (không hiện trong danh sách)</summary>
    Withdrawn,
}
