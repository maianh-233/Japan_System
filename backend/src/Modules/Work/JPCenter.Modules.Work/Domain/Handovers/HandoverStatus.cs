namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>Trạng thái bàn giao</summary>
public enum HandoverStatus
{
    /// <summary>Chờ ca sau nhận</summary>
    Pending,
    /// <summary>Đã nhận</summary>
    Received,
    /// <summary>Có khiếu nại (lệch tiền...)</summary>
    Disputed,
}
