using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Sessions.RevokeSession;

/// <summary>AUTH-08 — đăng xuất 1 thiết bị của tôi (chọn chính thiết bị đang dùng = đăng xuất như AUTH-03).</summary>
public sealed record RevokeSessionCommand(Guid SessionId) : ICommand;
