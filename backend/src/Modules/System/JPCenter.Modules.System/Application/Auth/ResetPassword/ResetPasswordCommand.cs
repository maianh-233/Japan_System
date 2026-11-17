using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Auth.ResetPassword;

/// <summary>AUTH-04 bước 3 — đặt mật khẩu mới bằng vé đã nhận ở bước 2.</summary>
public sealed record ResetPasswordCommand(string ResetToken, string NewPassword) : ICommand;
