using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Roles;

/// <summary>Vai trò = tập quyền kèm phạm vi. Bảng: system.roles (+ role_grants)</summary>
public sealed class Role : AggregateRoot<RoleId>, IAuditable, ISoftDeletable
{
    private readonly List<RoleGrant> _grants = [];

    private Role() { }

    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    /// <summary>Role mặc định của hệ thống — không xóa được</summary>
    public bool IsSystem { get; private set; }
    /// <summary>Toàn quyền (Super Admin) — bỏ qua ma trận quyền</summary>
    public bool IsSuperAdmin { get; private set; }
    public IReadOnlyList<RoleGrant> Grants => _grants;

    public static Result<Role> Create(string name, string description, bool isSystem = false, bool isSuperAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return RoleErrors.NameRequired;
        return new Role { Id = RoleId.New(), Name = name.Trim(), Description = description, IsSystem = isSystem, IsSuperAdmin = isSuperAdmin };
    }

    /// <summary>Đặt lại toàn bộ ma trận quyền: mã quyền → phạm vi</summary>
    public Result SetGrants(IReadOnlyDictionary<string, PermissionScope> grants)
    {
        if (IsSuperAdmin) return RoleErrors.SuperAdminLocked;
        _grants.Clear();
        _grants.AddRange(grants.Select(g => new RoleGrant(g.Key, g.Value)));
        return Result.Success();
    }

    public Result EnsureDeletable() => IsSystem ? RoleErrors.SystemRole : Result.Success();
}
