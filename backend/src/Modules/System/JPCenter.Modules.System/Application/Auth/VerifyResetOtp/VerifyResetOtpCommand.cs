using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Auth.VerifyResetOtp;

/// <summary>AUTH-04 bước 2 — OTP đúng → trả "vé" đặt mật khẩu (dùng 1 lần, hạn 10 phút).</summary>
public sealed record VerifyResetOtpCommand(string Email, string Otp) : ICommand<VerifyResetOtpResult>;

public sealed record VerifyResetOtpResult(string ResetToken);
