using FluentValidation;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Chưa nhập mật khẩu hiện tại").MaximumLength(128);
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
