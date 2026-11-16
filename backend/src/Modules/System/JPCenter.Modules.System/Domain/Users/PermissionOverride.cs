using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Users;

/// <summary>Cấp / chặn 1 quyền lẻ, có thời hạn. Bảng: system.permission_overrides</summary>
public sealed class PermissionOverride
{
    private PermissionOverride() { }

    internal PermissionOverride(string permission, OverrideEffect effect, PermissionScope? scope, DateTimeOffset? until,
        string reason, Guid grantedBy, DateTimeOffset grantedAt)
    {
        Id = IdGenerator.NewId();
        (Permission, Effect, Scope, Until, Reason, GrantedBy, GrantedAt) = (permission, effect, scope, until, reason, grantedBy, grantedAt);
    }

    public Guid Id { get; private set; }
    public string Permission { get; private set; } = null!;
    public OverrideEffect Effect { get; private set; }
    /// <summary>Chỉ khi Grant</summary>
    public PermissionScope? Scope { get; private set; }
    /// <summary>Hết hạn — null = vô thời hạn</summary>
    public DateTimeOffset? Until { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid GrantedBy { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
}
