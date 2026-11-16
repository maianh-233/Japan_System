namespace JPCenter.Modules.System.Domain.Approvals;

/// <summary>Loại thao tác cần người thứ 2 duyệt</summary>
public enum ApprovalKind
{
    /// <summary>Hoàn tiền học phí</summary>
    Refund,
    /// <summary>Hủy hóa đơn</summary>
    VoidInvoice,
    /// <summary>Cấp quyền tài chính</summary>
    FinancePermission,
}
