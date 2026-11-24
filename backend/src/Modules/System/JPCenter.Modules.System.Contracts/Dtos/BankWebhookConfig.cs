namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>
/// Cấu hình webhook ngân hàng (TH-07) lấy từ tích hợp <c>vietqr</c>: khóa ký = trường <c>checksum</c> (đã giải mã).
/// </summary>
/// <param name="Connected">Tích hợp <c>vietqr</c> đang kết nối (kể cả đang lỗi)</param>
/// <param name="Secret">Khóa ký HMAC; null = chưa cấu hình</param>
/// <param name="AutoMatch">Việc <c>autoMatch</c> bật — tắt thì giao dịch chờ đối soát tay</param>
public sealed record BankWebhookConfig(bool Connected, string? Secret, bool AutoMatch);
