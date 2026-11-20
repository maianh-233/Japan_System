using JPCenter.Application.Common.Realtime;

namespace JPCenter.Infrastructure.Common.Realtime;

/// <summary>Mặc định khi không có web host (job, test không bật SignalR): bỏ qua. Api.Common thay bằng bản SignalR.</summary>
internal sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task ToEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, string type, object payload, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ToAccountsAsync(IReadOnlyCollection<Guid> accountIds, string type, object payload, CancellationToken ct = default) =>
        Task.CompletedTask;
}
