using FluentValidation;

namespace JPCenter.Modules.System.Application.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Chưa nhập email hoặc tên đăng nhập").MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Chưa nhập mật khẩu").MaximumLength(128);
    }
}
