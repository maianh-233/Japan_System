using FluentValidation;

namespace JPCenter.Modules.System.Application.Auth.ForgotPassword;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() =>
        RuleFor(x => x.Email).NotEmpty().WithMessage("Chưa nhập email").EmailAddress().WithMessage("Email không đúng định dạng").MaximumLength(200);
}
