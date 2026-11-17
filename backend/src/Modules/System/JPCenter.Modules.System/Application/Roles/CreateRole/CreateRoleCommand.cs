using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Roles.Common;

namespace JPCenter.Modules.System.Application.Roles.CreateRole;

/// <summary>PQ-02 — tạo vai trò; tùy chọn sao chép quyền từ role có sẵn rồi chỉnh thêm.</summary>
public sealed record CreateRoleCommand(string Name, string? Description, Guid? CopyFromRoleId, Dictionary<string, string>? Grants)
    : ICommand<RoleSaveResult>;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa nhập tên vai trò").MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.Grants).Must(g => RoleGrants.IsScope(g.Value)).WithMessage("Phạm vi phải là own / branch / all");
    }
}
