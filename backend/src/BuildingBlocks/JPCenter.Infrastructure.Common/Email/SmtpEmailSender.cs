using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using JPCenter.Application.Common.Email;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Email;

/// <summary>
/// Gửi email qua SMTP (local: Mailpit, không cần đăng nhập). Gửi kèm 2 bản: chữ thuần + HTML (multipart/alternative),
/// <c>Reply-To</c> về hộp thư hỗ trợ, đánh dấu email tự động. Lỗi gửi chỉ ghi log — không làm hỏng thao tác chính.
/// Chưa cấu hình <c>Email:Host</c> → bỏ qua (ghi cảnh báo). ADR 0002: chưa cần MailKit.
/// </summary>
public sealed class SmtpEmailSender(EmailOptions options, EmailRenderer renderer, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, EmailContent content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            logger.LogWarning("Chưa cấu hình Email:Host — bỏ qua email \"{Subject}\"", content.Subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(options.From, options.FromName, Encoding.UTF8),
                Subject = content.Subject,
                SubjectEncoding = Encoding.UTF8,
                HeadersEncoding = Encoding.UTF8,
            };
            message.To.Add(to);
            if (!string.IsNullOrWhiteSpace(options.ReplyTo)) message.ReplyToList.Add(options.ReplyTo);
            // Email giao dịch tự động (RFC 3834) — trình trả lời tự động không phản hồi lại
            message.Headers.Add("Auto-Submitted", "auto-generated");

            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(renderer.Text(content), Encoding.UTF8, MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(renderer.Html(content), Encoding.UTF8, MediaTypeNames.Text.Html));

            using var client = new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
            if (!string.IsNullOrWhiteSpace(options.Username)) client.Credentials = new NetworkCredential(options.Username, options.Password);
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "Gửi email \"{Subject}\" tới {To} thất bại", content.Subject, to);
        }
    }
}
