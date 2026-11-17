using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Sessions.RevokeOtherSessions;

/// <summary>AUTH-08 — "Đăng xuất thiết bị khác": giữ lại thiết bị đang dùng. Trả số thiết bị đã đăng xuất.</summary>
public sealed record RevokeOtherSessionsCommand : ICommand<int>;
