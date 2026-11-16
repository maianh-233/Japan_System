namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Loại đơn</summary>
public enum HrRequestType
{
    /// <summary>Nghỉ phép</summary>
    Leave,
    /// <summary>Đổi ca</summary>
    ShiftSwap,
    /// <summary>Công tác</summary>
    BusinessTrip,
    /// <summary>Đi muộn / về sớm</summary>
    LateEarly,
    /// <summary>Đề xuất</summary>
    Proposal,
    /// <summary>Tạm ứng</summary>
    Advance,
}
