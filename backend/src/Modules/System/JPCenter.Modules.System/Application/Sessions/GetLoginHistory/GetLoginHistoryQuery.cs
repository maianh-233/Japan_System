using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Sessions.GetLoginHistory;

/// <summary>AUTH-09 — các lần đăng nhập / thao tác bảo mật của tôi trong 30 ngày (tối đa 50 dòng).</summary>
public sealed record GetLoginHistoryQuery : IQuery<LoginHistoryDto>;

/// <param name="RecentFailures">Số lần đăng nhập thất bại liên tiếp gần nhất — &gt; 0 thì frontend hiện cảnh báo vàng (AUTH-09.E1)</param>
public sealed record LoginHistoryDto(IReadOnlyList<LoginHistoryItem> Items, int RecentFailures);

public sealed record LoginHistoryItem(DateTimeOffset At, string Action, bool Success, string Ip, string Device, string? Detail);
