namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>Gửi email (local: Mailpit — xem tại http://localhost:8025). Lỗi gửi không làm hỏng thao tác chính.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct);
}
