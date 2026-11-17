using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Auth.Logout;

/// <summary>AUTH-03 — thu hồi phiên của thiết bị này (theo access token, hoặc refresh token trong cookie khi access đã hết hạn).</summary>
public sealed record LogoutCommand(string? RefreshToken) : ICommand;
