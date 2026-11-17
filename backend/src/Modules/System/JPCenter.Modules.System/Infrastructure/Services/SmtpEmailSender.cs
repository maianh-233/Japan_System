using System.Net.Mail;
using System.Text;
using JPCenter.Modules.System.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Gửi email qua SMTP (cấu hình <c>Email:*</c>; local là Mailpit, không cần đăng nhập). Gửi lỗi chỉ ghi log —
/// không làm hỏng thao tác chính (người dùng bấm "Gửi lại" được). ADR 0002: chưa cần MailKit.
/// </summary>
internal sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var host = configuration["Email:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("Chưa cấu hình Email:Host — bỏ qua email \"{Subject}\"", subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(host, configuration.GetValue("Email:Port", 25));
            using var message = new MailMessage
            {
                From = new MailAddress(configuration["Email:From"] ?? "no-reply@jpcenter.vn", configuration["Email:FromName"] ?? "JP Center",
                    Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
            };
            message.To.Add(to);
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "Gửi email \"{Subject}\" thất bại", subject);
        }
    }
}
