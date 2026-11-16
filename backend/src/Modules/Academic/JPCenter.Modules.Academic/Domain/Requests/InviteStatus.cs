namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>Trạng thái lời mời dạy thay</summary>
public enum InviteStatus
{
    /// <summary>Chờ trả lời</summary>
    Pending,
    /// <summary>Đã nhận</summary>
    Accepted,
    /// <summary>Từ chối</summary>
    Declined,
    /// <summary>Hết hiệu lực</summary>
    Expired,
}
