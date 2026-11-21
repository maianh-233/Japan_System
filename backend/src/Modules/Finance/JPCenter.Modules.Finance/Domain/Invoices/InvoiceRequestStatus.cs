namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Trạng thái yêu cầu duyệt</summary>
public enum InvoiceRequestStatus
{
    Pending,
    Approved,
    Rejected,
    /// <summary>Người tạo rút lại (TC-07.E3)</summary>
    Withdrawn,
}
