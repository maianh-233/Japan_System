namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Kết quả gửi 1 tin. Không có kênh / mọi kênh lỗi → <paramref name="Sent"/> = false (KHÔNG phải lỗi nghiệp vụ).</summary>
/// <param name="Channel">Kênh đã gửi thành công: "zalo" · "sms" · "email"; null = chưa gửi được</param>
/// <param name="Sandbox">Chế độ thử — không gửi thật (quy tắc vàng 3)</param>
public sealed record MessageOutcome(bool Sent, string? Channel, bool Sandbox, string Detail);
