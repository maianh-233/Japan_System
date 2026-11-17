using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Auth.ChangePassword;

/// <summary>AUTH-06 — đổi mật khẩu khi đang đăng nhập; tùy chọn đăng xuất các thiết bị khác.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, bool LogoutOtherDevices) : ICommand<ChangePasswordResult>;

/// <summary>Access token mới cho thiết bị hiện tại (token cũ hết hiệu lực vì đổi mật khẩu thu hồi mọi token).</summary>
public sealed record ChangePasswordResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, int LoggedOutDevices);
