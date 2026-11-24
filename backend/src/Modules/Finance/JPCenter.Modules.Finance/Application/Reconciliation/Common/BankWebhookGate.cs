using System.Security.Cryptography;
using System.Text;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.Configuration;

namespace JPCenter.Modules.Finance.Application.Reconciliation.Common;

/// <summary>
/// TH-07 / TC-10.E6 — kiểm webhook ngân hàng: khóa ký lấy từ tích hợp <c>vietqr</c> (trường <c>checksum</c>); tích hợp đang kết nối mà chưa có khóa
/// (vd mất khóa giải mã) → dùng khóa cũ <c>Finance:BankWebhookSecret</c> làm dự phòng. <c>vietqr</c> chưa kết nối → từ chối (503).
/// Chữ ký sai → ghi nhật ký tích hợp + 401. Header: <c>X-Signature: sha256=&lt;hex HMAC-SHA256(body)&gt;</c>.
/// </summary>
public sealed class BankWebhookGate(IIntegrationLog integrations, IConfiguration configuration)
{
    public const string IntegrationKey = "vietqr";
    public const string FallbackSecretKey = "Finance:BankWebhookSecret";

    public static readonly Error NotConnected = Error.Conflict("system.integration.not_connected", "Tích hợp VietQR chưa kết nối");
    public static readonly Error BadSignature = Error.Unauthorized("finance.bank_webhook.signature", "Chữ ký webhook không hợp lệ");

    /// <summary>Hợp lệ → việc <c>autoMatch</c> đang bật hay không (tắt = giao dịch chờ đối soát tay)</summary>
    public async Task<Result<bool>> CheckAsync(string body, string signature, CancellationToken ct)
    {
        var cfg = await integrations.BankWebhookAsync(ct);
        if (!cfg.Connected) return NotConnected;
        var secret = cfg.Secret ?? configuration[FallbackSecretKey];
        if (!Verify(body, signature, secret))
        {
            await integrations.WriteAsync(IntegrationKey, false, "Từ chối webhook ngân hàng: chữ ký không hợp lệ", ct);
            return BadSignature;
        }
        return cfg.AutoMatch;
    }

    /// <summary>So chữ ký HMAC-SHA256 (thời gian cố định). Chưa có khóa → từ chối.</summary>
    public static bool Verify(string body, string header, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || !header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = Sign(body, secret);
        var given = Encoding.ASCII.GetBytes(header["sha256=".Length..].Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), given);
    }

    /// <summary>Hex chữ thường của HMAC-SHA256(body) — ngân hàng / test ký theo cách này</summary>
    public static string Sign(string body, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}
