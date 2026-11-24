using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Gửi tin ra ngoài qua tích hợp (TH-06): Zalo OA → SMS dự phòng, OTP chỉ SMS, email theo việc riêng. Ghi <c>integration_logs</c> mỗi lần thử
/// (không ghi nội dung nhạy cảm). Không bao giờ ném lỗi nghiệp vụ — module gọi chỉ đọc <see cref="MessageOutcome"/>.
/// Gọi từ việc chạy nền (event handler / job) để không làm chậm thao tác chính (quy tắc vàng 4).
/// </summary>
public interface IMessageChannels
{
    Task<MessageOutcome> SendAsync(OutgoingMessage message, CancellationToken ct);
}
