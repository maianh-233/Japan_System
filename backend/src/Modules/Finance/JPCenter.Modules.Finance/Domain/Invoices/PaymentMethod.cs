namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Hình thức thanh toán</summary>
public enum PaymentMethod
{
    /// <summary>Tiền mặt</summary>
    Cash,
    /// <summary>Chuyển khoản</summary>
    Transfer,
    /// <summary>VietQR — tên thành viên viết "Vietqr" để giá trị lưu / JSON là <c>vietqr</c> (chốt user story 08)</summary>
    Vietqr,
    /// <summary>Thẻ</summary>
    Card,
}
