using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Roles.Common;

namespace JPCenter.Modules.System.Application.Roles.UpdateRoleGrants;

/// <summary>PQ-03 — lưu toàn bộ ma trận quyền của role (mã quyền → own / branch / all; không có = không có quyền).</summary>
/// <param name="Version">Version nhận khi tải role — lệch nghĩa là người khác vừa sửa (E6).</param>
public sealed record UpdateRoleGrantsCommand(Guid RoleId, Dictionary<string, string> Grants, uint Version) : ICommand<RoleSaveResult>;

public sealed class UpdateRoleGrantsValidator : AbstractValidator<UpdateRoleGrantsCommand>
{
    public UpdateRoleGrantsValidator() =>
        RuleForEach(x => x.Grants).Must(g => RoleGrants.IsScope(g.Value)).WithMessage("Phạm vi phải là own / branch / all");
}
