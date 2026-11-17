using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.RefreshSession;

/// <summary>AUTH-02 — đổi refresh token (cookie) lấy access token mới; refresh token xoay vòng mỗi lần.</summary>
public sealed record RefreshSessionCommand(string? RefreshToken) : ICommand<SignInResult>;
