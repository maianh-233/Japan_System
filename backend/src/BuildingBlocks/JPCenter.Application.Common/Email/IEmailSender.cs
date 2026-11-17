namespace JPCenter.Application.Common.Email;

/// <summary>
/// Gửi email giao dịch (local: Mailpit — xem tại http://localhost:8025). Bên gửi áp khung thương hiệu chung
/// (logo, màu, chân trang hotline / email hỗ trợ) lên <see cref="EmailContent"/>, gửi kèm bản HTML + chữ thuần.
/// Lỗi gửi không làm hỏng thao tác chính.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, EmailContent content, CancellationToken ct);
}
