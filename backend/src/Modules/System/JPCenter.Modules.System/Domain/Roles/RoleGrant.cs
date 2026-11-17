using JPCenter.SharedKernel.Primitives;
namespace JPCenter.Modules.System.Domain.Roles;

/// <summary>1 quyền trong role. Bảng: system.role_grants (khóa: role_id + permission)</summary>
public sealed class RoleGrant
{
    private RoleGrant() { }

    internal RoleGrant(string permission, PermissionScope scope) => (Permission, Scope) = (permission, scope);

    /// <summary>Mã quyền "module.page.action", vd crm.leads.update</summary>
    public string Permission { get; private set; } = null!;
    public PermissionScope Scope { get; private set; }
}
