using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>Nhật ký + cấu hình tích hợp cho module nhận dữ liệu từ ngoài vào (webhook ngân hàng — TH-07, TC-10).</summary>
public interface IIntegrationLog
{
    /// <summary>Ghi 1 dòng nhật ký cho tích hợp <paramref name="key"/> (vd <c>vietqr</c>); khóa không có → bỏ qua</summary>
    Task WriteAsync(string key, bool ok, string message, CancellationToken ct);

    /// <summary>Cấu hình webhook ngân hàng từ tích hợp <c>vietqr</c></summary>
    Task<BankWebhookConfig> BankWebhookAsync(CancellationToken ct);
}
