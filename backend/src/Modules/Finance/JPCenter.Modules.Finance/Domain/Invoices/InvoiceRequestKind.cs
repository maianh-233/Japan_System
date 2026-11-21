namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Loại yêu cầu duyệt</summary>
public enum InvoiceRequestKind
{
    /// <summary>Hoàn tiền (từ ngưỡng — TC-07)</summary>
    Refund,
    /// <summary>Hủy hóa đơn lập sai (TC-08)</summary>
    Void,
}
