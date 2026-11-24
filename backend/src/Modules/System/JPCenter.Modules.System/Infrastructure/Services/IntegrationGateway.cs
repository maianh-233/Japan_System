using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Net.Mime;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JPCenter.Application.Common.Email;
using JPCenter.Infrastructure.Common.Email;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static JPCenter.Modules.System.Domain.Integrations.IntegrationKeys;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IIntegrationGateway"/> (story 15, quy tắc vàng 3 — chi phí):
/// <list type="bullet">
/// <item>Zalo OA, SMS: mặc định <b>chế độ thử</b> (không gửi, ghi "(thử)"). Gửi thật (<c>Integrations:&lt;key&gt;:Live = true</c>) chưa hỗ trợ —
/// cần tài khoản ZNS / Brandname và mẫu tin đã duyệt (ghi lỗi rõ ràng, không giả vờ gửi).</item>
/// <item>Email: thật = SMTP theo cấu hình tích hợp; chế độ thử (mặc định ở Development) = gửi qua máy chủ email của hệ thống
/// (<c>Email:*</c>, local Mailpit).</item>
/// <item>Zoom: Server-to-Server OAuth (lấy token, tạo phòng định kỳ không giờ cố định). Chế độ thử (<c>Integrations:zoom:Live = false</c>) trả link giả "(thử)".</item>
/// <item>Google Meet: chưa hỗ trợ tạo phòng tự động (cần tài khoản dịch vụ Google Workspace) — báo lỗi để lớp hiện "Chưa có link phòng".</item>
/// <item>VietQR: kiểm tra = ký thử HMAC bằng checksum key. Nguồn lead: chỉ nhận webhook, không có lệnh gọi thử.</item>
/// </list>
/// </summary>
internal sealed class IntegrationGateway(
    IHttpClientFactory http, IConfiguration configuration, IHostEnvironment env, IEmailSender systemEmail, EmailRenderer renderer,
    EmailOptions emailOptions, IAppLinks links, ILogger<IntegrationGateway> logger) : IIntegrationGateway
{
    public const string HttpClientName = "integrations";
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private const string ZoomTokenUrl = "https://zoom.us/oauth/token";
    private const string ZoomMeetingsUrl = "https://api.zoom.us/v2/users/me/meetings";
    private const string LiveNotSupported = "Chưa hỗ trợ gửi thật qua {0} — cần tài khoản và mẫu tin đã duyệt; tắt Integrations:{1}:Live để dùng chế độ thử";

    /// <summary>Gọi thật hay chế độ thử. Mặc định: Zalo / SMS thử; email thử ở Development; còn lại thật.</summary>
    public bool IsLive(string key) => configuration.GetValue<bool?>($"Integrations:{key}:Live") ?? key switch
    {
        ZaloOa or Sms => false,
        Email => !env.IsDevelopment(),
        _ => true,
    };

    public async Task<GatewayResult> TestAsync(string key, IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        var live = IsLive(key);
        switch (key)
        {
            case ZaloOa:
                return live ? GatewayResult.Fail(string.Format(LiveNotSupported, "Zalo OA", key)) : GatewayResult.Success("Lấy thông tin OA thành công", sandbox: true);
            case Sms:
                return live ? GatewayResult.Fail(string.Format(LiveNotSupported, "SMS Brandname", key)) : GatewayResult.Success("Kiểm tra số dư thành công", sandbox: true);
            case Email:
                return live ? await ProbeSmtpAsync(config, ct) : GatewayResult.Success("Gửi qua máy chủ email của hệ thống", sandbox: true);
            case VietQr:
                var checksum = config.GetValueOrDefault("checksum");
                if (string.IsNullOrEmpty(checksum)) return GatewayResult.Fail("Thiếu checksum key", authFailure: true);
                _ = HMACSHA256.HashData(Encoding.UTF8.GetBytes(checksum), "ping"u8);
                return GatewayResult.Success("Ký thử checksum thành công");
            case Zoom:
                if (!live) return GatewayResult.Success("Lấy token Zoom thành công", sandbox: true);
                var token = await ZoomTokenAsync(config, ct);
                return token.Ok ? GatewayResult.Success("Lấy token Zoom thành công") : token;
            case Meet:
                return MeetNotSupported;
            default:
                return GatewayResult.Success("Đã lưu cấu hình — nguồn lead gửi dữ liệu vào webhook, không có lệnh gọi thử");
        }
    }

    public async Task<GatewayResult> SendAsync(MessageChannel channel, IReadOnlyDictionary<string, string> config, OutgoingMessage message,
        CancellationToken ct)
    {
        switch (channel)
        {
            case MessageChannel.Zalo:
                return IsLive(ZaloOa)
                    ? GatewayResult.Fail(string.Format(LiveNotSupported, "Zalo OA", ZaloOa))
                    : GatewayResult.Success($"Đã gửi ZNS \"{message.EventKey}\" tới {MaskPhone(message.Phone)}", sandbox: true);
            case MessageChannel.Sms:
                return IsLive(Sms)
                    ? GatewayResult.Fail(string.Format(LiveNotSupported, "SMS Brandname", Sms))
                    : GatewayResult.Success($"Đã gửi SMS \"{message.EventKey}\" tới {MaskPhone(message.Phone)}", sandbox: true);
            default:
                var content = ToEmail(message);
                var to = MaskEmail(message.Email);
                if (!IsLive(Email))
                {
                    await systemEmail.SendAsync(message.Email!, content, ct);
                    return GatewayResult.Success($"Đã gửi email \"{message.EventKey}\" tới {to}", sandbox: true);
                }
                return await SendSmtpAsync(config, message.Email!, content, ct) is { } error
                    ? error
                    : GatewayResult.Success($"Đã gửi email \"{message.EventKey}\" tới {to}");
        }
    }

    public async Task<GatewayResult> CreateRoomAsync(string key, IReadOnlyDictionary<string, string> config, string topic, CancellationToken ct)
    {
        if (key == Meet) return MeetNotSupported;
        if (key != Zoom) return GatewayResult.Fail("Tích hợp này không tạo phòng học");
        if (!IsLive(Zoom))
            return GatewayResult.Success($"Đã tạo phòng Zoom \"{topic}\"", $"https://zoom.us/j/9{Random.Shared.NextInt64(100_000_000, 999_999_999)}", sandbox: true);

        var token = await ZoomTokenAsync(config, ct);
        if (!token.Ok) return token;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, ZoomMeetingsUrl)
            {
                // type 3 = phòng định kỳ không giờ cố định — 1 link dùng cho cả lớp (TH-08)
                Content = JsonContent.Create(new { topic, type = 3, settings = new { join_before_host = false, waiting_room = true } }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
            using var res = await http.CreateClient(HttpClientName).SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return ZoomError("Tạo phòng Zoom", res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("join_url", out var url) && url.GetString() is { Length: > 0 } link
                ? GatewayResult.Success($"Đã tạo phòng Zoom \"{topic}\"", link)
                : GatewayResult.Fail("Zoom không trả link phòng");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Tạo phòng Zoom lỗi");
            return GatewayResult.Fail("Không gọi được Zoom (mạng / hết thời gian chờ)");
        }
    }

    private static readonly GatewayResult MeetNotSupported =
        GatewayResult.Fail("Chưa hỗ trợ tạo phòng Google Meet tự động (cần tài khoản dịch vụ Google Workspace) — nhập link tay ở lớp");

    /// <summary>Zoom Server-to-Server OAuth: token ở <see cref="GatewayResult.Value"/></summary>
    private async Task<GatewayResult> ZoomTokenAsync(IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        var (account, client, secret) = (config.GetValueOrDefault("accountId"), config.GetValueOrDefault("clientId"), config.GetValueOrDefault("clientSecret"));
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(client) || string.IsNullOrEmpty(secret))
            return GatewayResult.Fail("Thiếu Account ID / Client ID / Client secret", authFailure: true);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"{ZoomTokenUrl}?grant_type=account_credentials&account_id={Uri.EscapeDataString(account)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{client}:{secret}")));
            using var res = await http.CreateClient(HttpClientName).SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return ZoomError("Lấy token Zoom", res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("access_token", out var t) && t.GetString() is { Length: > 0 } token
                ? GatewayResult.Success("Lấy token Zoom thành công", token)
                : GatewayResult.Fail("Zoom không trả token", authFailure: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Lấy token Zoom lỗi");
            return GatewayResult.Fail("Không gọi được Zoom (mạng / hết thời gian chờ)");
        }
    }

    /// <summary>400 / 401 / 403 từ Zoom = sai khóa → lỗi xác thực; còn lại là lỗi tạm</summary>
    private static GatewayResult ZoomError(string what, HttpStatusCode status) =>
        status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? GatewayResult.Fail($"{what}: sai thông tin xác thực ({(int)status})", authFailure: true)
            : GatewayResult.Fail($"{what}: Zoom trả lỗi {(int)status}");

    /// <summary>TH-04 email — mở kết nối tới máy chủ SMTP (không gửi thư)</summary>
    private static async Task<GatewayResult> ProbeSmtpAsync(IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        var host = config.GetValueOrDefault("host");
        if (string.IsNullOrEmpty(host) || !int.TryParse(config.GetValueOrDefault("port"), out var port))
            return GatewayResult.Fail("Thiếu máy chủ / cổng SMTP", authFailure: true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, timeout.Token);
            return GatewayResult.Success($"Kết nối SMTP {host}:{port} thành công");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return GatewayResult.Fail($"Không kết nối được SMTP {host}:{port}");
        }
    }

    /// <summary>Gửi thật qua SMTP của tích hợp; lỗi → kết quả lỗi (sai tài khoản = lỗi xác thực)</summary>
    private async Task<GatewayResult?> SendSmtpAsync(IReadOnlyDictionary<string, string> config, string to, EmailContent content, CancellationToken ct)
    {
        var host = config.GetValueOrDefault("host");
        if (string.IsNullOrEmpty(host) || !int.TryParse(config.GetValueOrDefault("port"), out var port))
            return GatewayResult.Fail("Thiếu máy chủ / cổng SMTP", authFailure: true);
        var user = config.GetValueOrDefault("user");
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(string.IsNullOrEmpty(user) || !user.Contains('@') ? emailOptions.From : user, emailOptions.FromName, Encoding.UTF8),
                Subject = content.Subject, SubjectEncoding = Encoding.UTF8, HeadersEncoding = Encoding.UTF8,
            };
            message.To.Add(to);
            if (!string.IsNullOrWhiteSpace(emailOptions.ReplyTo)) message.ReplyToList.Add(emailOptions.ReplyTo);
            message.Headers.Add("Auto-Submitted", "auto-generated");
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(renderer.Text(content), Encoding.UTF8, MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(renderer.Html(content), Encoding.UTF8, MediaTypeNames.Text.Html));
            using var client = new SmtpClient(host, port) { EnableSsl = port is not (25 or 1025), Timeout = 15_000 };
            if (!string.IsNullOrEmpty(user)) client.Credentials = new NetworkCredential(user, config.GetValueOrDefault("pass"));
            await client.SendMailAsync(message, ct);
            return null;
        }
        catch (SmtpException ex) when (ex.StatusCode is SmtpStatusCode.MustIssueStartTlsFirst or SmtpStatusCode.ClientNotPermitted
                                           || ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return GatewayResult.Fail("SMTP từ chối đăng nhập — kiểm tra tài khoản / mật khẩu", authFailure: true);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException or IOException)
        {
            logger.LogWarning(ex, "Gửi email qua SMTP tích hợp lỗi");
            return GatewayResult.Fail("Gửi email lỗi (máy chủ SMTP)");
        }
    }

    private EmailContent ToEmail(OutgoingMessage m)
    {
        var content = EmailContent.Create(m.Title, m.Title, m.Lines.FirstOrDefault() ?? m.Title).Hello(m.Name);
        foreach (var line in m.Lines) content.Text(line);
        if (m.Details is { Count: > 0 } d) content.Details(d.Select(x => (x.Key, x.Value)).ToArray());
        if (!string.IsNullOrEmpty(m.Link)) content.Button("Xem chi tiết", links.Page(m.Link));
        return content;
    }

    /// <summary>Chỉ ghi 4 số cuối SĐT (quy tắc vàng 2)</summary>
    public static string MaskPhone(string? phone) => phone is { Length: > 4 } p ? "***" + p[^4..] : "***";

    public static string MaskEmail(string? email) =>
        email is { } e && e.IndexOf('@') is > 0 and var at ? e[0] + "***" + e[at..] : "***";
}
