using FluentValidation;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Thiếu mã kích hoạt").MaximumLength(200);
        RuleFor(x => x.Password).StrongPassword();
    }
}
