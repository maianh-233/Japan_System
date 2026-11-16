namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Hình thức thanh toán</summary>
public enum PaymentMethod
{
    /// <summary>Tiền mặt</summary>
    Cash,
    /// <summary>Chuyển khoản</summary>
    Transfer,
    /// <summary>VietQR</summary>
    VietQr,
    /// <summary>Thẻ</summary>
    Card,
}
