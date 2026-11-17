using FluentValidation;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.ResetPassword;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty().WithMessage("Bạn cần xác thực OTP trước").MaximumLength(200);
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
