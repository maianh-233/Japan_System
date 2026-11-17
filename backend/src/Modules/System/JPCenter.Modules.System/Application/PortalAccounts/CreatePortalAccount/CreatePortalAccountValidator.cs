using FluentValidation;

namespace JPCenter.Modules.System.Application.PortalAccounts.CreatePortalAccount;

public sealed class CreatePortalAccountValidator : AbstractValidator<CreatePortalAccountCommand>
{
    public CreatePortalAccountValidator()
    {
        RuleFor(x => x.OwnerType).Must(t => t is "student" or "guardian").WithMessage("Loại chủ tài khoản phải là học viên hoặc phụ huynh");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Thiếu hồ sơ chủ tài khoản");
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Thiếu tên chủ tài khoản").MaximumLength(150);
        RuleFor(x => x.Username).NotEmpty().WithMessage("Chưa nhập tên đăng nhập")
            .Matches("^[a-zA-Z0-9._-]{3,50}$").WithMessage("Tên đăng nhập 3–50 ký tự: chữ không dấu, số, dấu chấm, gạch");
        RuleFor(x => x.Email).EmailAddress().WithMessage("Email không đúng định dạng").MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
