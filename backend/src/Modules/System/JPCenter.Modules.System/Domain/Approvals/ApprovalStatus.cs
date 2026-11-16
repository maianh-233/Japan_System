namespace JPCenter.Modules.System.Domain.Approvals;

/// <summary>Trạng thái yêu cầu duyệt</summary>
public enum ApprovalStatus
{
    /// <summary>Chờ duyệt</summary>
    Pending,
    /// <summary>Đã duyệt</summary>
    Approved,
    /// <summary>Từ chối</summary>
    Rejected,
    /// <summary>Người tạo rút lại</summary>
    Withdrawn,
}
