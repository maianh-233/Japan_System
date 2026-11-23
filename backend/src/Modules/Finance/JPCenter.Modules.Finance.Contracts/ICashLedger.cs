namespace JPCenter.Modules.Finance.Contracts;

/// <summary>Tiền mặt tại quầy cho bàn giao ca (BG-01 bước 2): Work tính "tiền mặt hệ thống" — không cho nhập tay.</summary>
public interface ICashLedger
{
    /// <summary>Tiền mặt thu (trừ tiền mặt đã hoàn) của hóa đơn thuộc chi nhánh trong khoảng [<paramref name="from"/>, <paramref name="to"/>), VND</summary>
    Task<decimal> CashCollectedAsync(Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
