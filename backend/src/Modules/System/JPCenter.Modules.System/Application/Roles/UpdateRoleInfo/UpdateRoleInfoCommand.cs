using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Roles.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Roles.UpdateRoleInfo;

/// <summary>Đổi tên / mô tả vai trò (role mặc định chỉ sửa mô tả — PQ-03.E7).</summary>
public sealed record UpdateRoleInfoCommand(Guid RoleId, string Name, string? Description) : ICommand<RoleDto>;

public sealed class UpdateRoleInfoValidator : AbstractValidator<UpdateRoleInfoCommand>
{
    public UpdateRoleInfoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa nhập tên vai trò").MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateRoleInfoHandler(ISystemDbContext db, RoleReader reader, IAuditTrail audit) : ICommandHandler<UpdateRoleInfoCommand, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(UpdateRoleInfoCommand cmd, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == new RoleId(cmd.RoleId), ct);
        if (role is null) return PermissionErrors.RoleNotFound;
        var name = cmd.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Id != role.Id && r.Name.ToLower() == name.ToLower(), ct)) return PermissionErrors.DuplicateRoleName;

        var oldName = role.Name;
        if (role.Rename(name, cmd.Description ?? "") is { IsFailure: true } failed) return failed.Error;
        await db.SaveChangesAsync(ct);

        audit.Record(AuditCategory.Permission, AuditSeverity.Info, "Sửa thông tin vai trò", oldName == name ? name : $"{oldName} → {name}");
        return await reader.GetAsync(role.Id, ct);
    }
}
