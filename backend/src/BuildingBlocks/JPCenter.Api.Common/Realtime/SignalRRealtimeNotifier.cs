using JPCenter.Application.Common.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace JPCenter.Api.Common.Realtime;

/// <summary>Hiện thực <see cref="IRealtimeNotifier"/> qua <see cref="RealtimeHub"/>. Lỗi đẩy realtime chỉ ghi log — không làm hỏng thao tác đã lưu.</summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<RealtimeHub> hub, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public Task ToEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, string type, object payload, CancellationToken ct = default) =>
        SendAsync(employeeIds.Distinct().Select(RealtimeHub.EmployeeGroup).ToList(), type, payload, ct);

    public Task ToAccountsAsync(IReadOnlyCollection<Guid> accountIds, string type, object payload, CancellationToken ct = default) =>
        SendAsync(accountIds.Distinct().Select(RealtimeHub.AccountGroup).ToList(), type, payload, ct);

    private async Task SendAsync(IReadOnlyList<string> groups, string type, object payload, CancellationToken ct)
    {
        if (groups.Count == 0) return;
        try
        {
            await hub.Clients.Groups(groups).SendAsync(type, payload, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không đẩy được sự kiện realtime {Type} tới {Count} nhóm", type, groups.Count);
        }
    }
}
