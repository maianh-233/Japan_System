namespace JPCenter.Modules.System.Domain.OneTimeTokens;

/// <summary>Mục đích mã dùng 1 lần</summary>
public enum TokenPurpose
{
    /// <summary>Link kích hoạt tài khoản được mời</summary>
    Invite,
    /// <summary>OTP quên mật khẩu</summary>
    PasswordResetOtp,
    /// <summary>Vé đặt mật khẩu mới (sau khi OTP đúng)</summary>
    PasswordResetGrant,
    /// <summary>Mã 2FA khi đăng nhập thiết bị mới</summary>
    TwoFactor,
    /// <summary>Mã xác nhận khi bật 2FA</summary>
    TwoFactorSetup,
}
