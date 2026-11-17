using FluentValidation;

namespace JPCenter.Modules.System.Application.Auth.VerifyResetOtp;

public sealed class VerifyResetOtpValidator : AbstractValidator<VerifyResetOtpCommand>
{
    public VerifyResetOtpValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Chưa nhập email").MaximumLength(200);
        RuleFor(x => x.Otp).NotEmpty().WithMessage("Chưa nhập mã OTP").Matches("^[0-9]{6}$").WithMessage("Mã OTP gồm 6 chữ số");
    }
}
