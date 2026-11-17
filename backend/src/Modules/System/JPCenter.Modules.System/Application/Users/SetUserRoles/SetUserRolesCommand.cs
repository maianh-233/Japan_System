using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Users.Common;

namespace JPCenter.Modules.System.Application.Users.SetUserRoles;

/// <summary>PQ-05 — thay toàn bộ vai trò của 1 người (mỗi dòng: role + chi nhánh, rỗng = mọi chi nhánh). Gỡ hết được (E7).</summary>
public sealed record SetUserRolesCommand(Guid AccountId, List<RoleAssignmentInput> Roles) : ICommand<UserChangeResult>;

public sealed class SetUserRolesValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesValidator()
    {
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles).Must(r => r.RoleId != Guid.Empty && r.BranchIds is not null).WithMessage("Dòng vai trò không hợp lệ");
    }
}
