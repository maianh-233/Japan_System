namespace JPCenter.Modules.Reporting.Domain.TodayWork;

/// <summary>Mức độ 1 việc (BC-02) — thứ tự enum = thứ tự hiển thị: đỏ → vàng → xanh</summary>
public enum WorkTone
{
    /// <summary>Đỏ — đã trễ</summary>
    Danger,
    /// <summary>Vàng — gấp, sắp trễ</summary>
    Warning,
    /// <summary>Xanh — sắp tới</summary>
    Info,
}
