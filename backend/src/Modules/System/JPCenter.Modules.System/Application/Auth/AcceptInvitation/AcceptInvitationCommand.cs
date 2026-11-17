using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.AcceptInvitation;

/// <summary>AUTH-05 bước 3 — đặt mật khẩu qua link mời → tài khoản "active" → tự đăng nhập.</summary>
public sealed record AcceptInvitationCommand(string Token, string Password) : ICommand<SignInResult>;
