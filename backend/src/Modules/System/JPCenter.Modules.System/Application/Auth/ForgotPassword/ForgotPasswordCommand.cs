using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Auth.ForgotPassword;

/// <summary>AUTH-04 bước 1 — gửi OTP 6 số về email. Luôn phản hồi như nhau dù email có tồn tại hay không.</summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand<ForgotPasswordResult>;

/// <param name="MaskedEmail">Email đã che, vd "ng****@gmail.com"</param>
/// <param name="ResendAfter">Số giây trước khi được gửi lại</param>
public sealed record ForgotPasswordResult(string MaskedEmail, int ResendAfter);
