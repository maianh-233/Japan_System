namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>Tuổi nợ (TC-11 bước 1) tính từ hạn đang áp dụng của hóa đơn còn nợ</summary>
public enum DebtAging
{
    /// <summary>Chưa tới hạn</summary>
    NotDue,
    /// <summary>Quá hạn 1–30 ngày</summary>
    Days1To30,
    /// <summary>Quá hạn 31–60 ngày</summary>
    Days31To60,
    /// <summary>Quá hạn &gt; 60 ngày</summary>
    Over60,
}
