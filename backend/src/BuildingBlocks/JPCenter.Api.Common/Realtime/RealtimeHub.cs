using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Security;
using Microsoft.AspNetCore.SignalR;

namespace JPCenter.Api.Common.Realtime;

/// <summary>
/// Hub realtime chung <c>/hubs/realtime</c> — server chỉ ĐẨY xuống (client không gọi method nào).
/// Kết nối đi qua cùng pipeline xác thực (token ở header hoặc query <c>access_token</c> cho WebSocket) → vào nhóm theo tài khoản / nhân viên.
/// </summary>
public sealed class RealtimeHub : Hub
{
    public const string Path = "/hubs/realtime";

    public static string AccountGroup(Guid accountId) => $"acc:{accountId:N}";
    public static string EmployeeGroup(Guid employeeId) => $"emp:{employeeId:N}";

    public override async Task OnConnectedAsync()
    {
        if (Context.GetHttpContext()?.Items[HttpCurrentUser.AccessKey] is not UserAccess access)
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(access.AccountId));
        if (access.EmployeeId is { } emp) await Groups.AddToGroupAsync(Context.ConnectionId, EmployeeGroup(emp));
        await base.OnConnectedAsync();
    }
}
