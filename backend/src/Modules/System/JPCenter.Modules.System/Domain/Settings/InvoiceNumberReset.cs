namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Đánh số lại mã hóa đơn từ 1 mỗi kỳ</summary>
public enum InvoiceNumberReset
{
    /// <summary>Không bao giờ</summary>
    Never,
    /// <summary>Mỗi năm</summary>
    Year,
    /// <summary>Mỗi tháng</summary>
    Month,
}
