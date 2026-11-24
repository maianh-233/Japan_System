using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IIntegrationLog"/>: nhật ký cho dữ liệu đi vào (webhook ngân hàng — TH-07) + khóa ký webhook từ <c>vietqr.checksum</c>.
/// </summary>
internal sealed class IntegrationLogWriter(IntegrationCalls calls, IClock clock) : IIntegrationLog
{
    public const string AutoMatch = "autoMatch";

    public async Task WriteAsync(string key, bool ok, string message, CancellationToken ct)
    {
        if (await calls.FindAsync(key, ct) is not { } integration) return;
        if (ok) integration.MarkSynced(clock.UtcNow);
        calls.Note(integration, ok, message);
        await calls.SaveAsync(ct);
    }

    public async Task<BankWebhookConfig> BankWebhookAsync(CancellationToken ct)
    {
        if (await calls.FindAsync(IntegrationKeys.VietQr, ct) is not { } vietqr || vietqr.Status == IntegrationStatus.Disconnected)
            return new BankWebhookConfig(false, null, false);
        var secret = calls.ConfigOf(vietqr).GetValueOrDefault("checksum");
        return new BankWebhookConfig(true, string.IsNullOrEmpty(secret) ? null : secret, vietqr.Events.GetValueOrDefault(AutoMatch));
    }
}
