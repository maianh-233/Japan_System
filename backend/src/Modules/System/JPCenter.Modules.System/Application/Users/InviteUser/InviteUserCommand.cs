using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Users.Common;

namespace JPCenter.Modules.System.Application.Users.InviteUser;

/// <summary>PQ-08 — cấp tài khoản cho nhân viên: tạo tài khoản "đã mời" + vai trò ban đầu, gửi email kích hoạt (AUTH-05).</summary>
public sealed record InviteUserCommand(Guid EmployeeId, List<RoleAssignmentInput> Roles) : ICommand<UserChangeResult>;

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Chọn vai trò ban đầu");
        RuleForEach(x => x.Roles).Must(r => r.RoleId != Guid.Empty && r.BranchIds is not null).WithMessage("Dòng vai trò không hợp lệ");
    }
}
