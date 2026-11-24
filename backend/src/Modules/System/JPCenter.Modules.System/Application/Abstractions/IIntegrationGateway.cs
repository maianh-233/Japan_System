using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Integrations;

namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>
/// Gọi dịch vụ ngoài theo cấu hình tích hợp ĐÃ GIẢI MÃ (story 15). Không ném exception cho lỗi mạng / dịch vụ — trả <see cref="GatewayResult"/>.
/// Zalo OA, SMS mặc định chế độ thử; bật thật bằng cấu hình <c>Integrations:&lt;key&gt;:Live = true</c>.
/// </summary>
public interface IIntegrationGateway
{
    /// <summary>TH-04 — gọi thử (Zalo: thông tin OA · SMS: số dư · SMTP: kết nối · Zoom: lấy token · VietQR: ký thử checksum)</summary>
    Task<GatewayResult> TestAsync(string key, IReadOnlyDictionary<string, string> config, CancellationToken ct);

    /// <summary>TH-06 — gửi 1 tin qua kênh</summary>
    Task<GatewayResult> SendAsync(MessageChannel channel, IReadOnlyDictionary<string, string> config, OutgoingMessage message, CancellationToken ct);

    /// <summary>TH-08 — tạo phòng học định kỳ (Zoom / Meet); link ở <see cref="GatewayResult.Value"/></summary>
    Task<GatewayResult> CreateRoomAsync(string key, IReadOnlyDictionary<string, string> config, string topic, CancellationToken ct);
}
