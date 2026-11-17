using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Sessions.GetMySessions;

/// <summary>AUTH-08 — các thiết bị đang đăng nhập của tôi (phiên hết hạn không hiện — E2).</summary>
public sealed record GetMySessionsQuery : IQuery<IReadOnlyList<MySessionDto>>;

/// <param name="Current">Thiết bị đang dùng ("Thiết bị này")</param>
public sealed record MySessionDto(Guid Id, string Device, string Ip, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool Remember, bool Current);
