using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.System.Domain.Common;
using JPCenter.Modules.System.Domain.Roles;

namespace JPCenter.Modules.System.Domain.Users;

/// <summary>
/// Tài khoản đăng nhập của nhân viên / giáo viên (1–1 với Hr.Employee). Bảng: system.user_accounts
/// (+ user_role_assignments, permission_overrides).
/// Quyền hiệu lực = hợp các role (phạm vi rộng nhất) + cấp lẻ − chặn lẻ.
/// </summary>
public sealed class UserAccount : AggregateRoot<UserAccountId>, IAuditable
{
    private readonly List<UserRoleAssignment> _roles = [];
    private readonly List<PermissionOverride> _overrides = [];

    private UserAccount() { }

    /// <summary>Hr.Employee — duy nhất</summary>
    public Guid EmployeeId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public UserAccountStatus Status { get; private set; }
    public bool TwoFactorEnabled { get; private set; }
    public string? TwoFactorSecret { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public string? LockedReason { get; private set; }
    /// <summary>Khóa tạm khi nhập sai nhiều lần (owned: failed_login_count, locked_until) — AUTH-11</summary>
    public LoginThrottle Throttle { get; private set; } = LoginThrottle.None;
    /// <summary>Buộc đổi mật khẩu ở lần đăng nhập kế tiếp (mật khẩu tạm do quản trị đặt)</summary>
    public bool MustChangePassword { get; private set; }
    /// <summary>Bắt buộc 2FA (quản lý, kế toán) — chưa bật thì phải thiết lập trước khi vào — AUTH-07.E1</summary>
    public bool TwoFactorRequired { get; private set; }
    public IReadOnlyList<UserRoleAssignment> Roles => _roles;
    public IReadOnlyList<PermissionOverride> Overrides => _overrides;

    public static Result<UserAccount> Invite(Guid employeeId, string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return UserAccountErrors.EmailRequired;
        return new UserAccount { Id = UserAccountId.New(), EmployeeId = employeeId, Email = email.Trim().ToLowerInvariant(), Status = UserAccountStatus.Invited };
    }

    /// <summary>Người dùng tự đặt / đổi / đặt lại mật khẩu: kích hoạt nếu đang được mời, xóa khóa tạm</summary>
    public void SetPassword(string passwordHash)
    {
        (PasswordHash, MustChangePassword, Throttle) = (passwordHash, false, LoginThrottle.None);
        if (Status == UserAccountStatus.Invited) Status = UserAccountStatus.Active;
    }

    /// <summary>Quản trị đặt mật khẩu tạm → buộc đổi khi đăng nhập</summary>
    public void SetTemporaryPassword(string passwordHash) => (PasswordHash, MustChangePassword) = (passwordHash, true);

    /// <summary>Kiểm tra trước khi so mật khẩu: khóa tạm / trạng thái</summary>
    public Result CanSignIn(DateTimeOffset now) =>
        Throttle.EnsureNotLocked(now) is { IsFailure: true } r ? r
        : Status switch
        {
            UserAccountStatus.Locked => UserAccountErrors.AccountLocked,
            UserAccountStatus.Invited => UserAccountErrors.AccountInvited,
            _ => Result.Success(),
        };

    /// <summary>Sai mật khẩu → tăng bộ đếm; trả true nếu vừa bị khóa tạm</summary>
    public bool RecordFailedLogin(DateTimeOffset now)
    {
        Throttle = Throttle.Fail(now);
        return Throttle.JustLocked(now);
    }

    public void RequireTwoFactor(bool required) => TwoFactorRequired = required;

    public void AssignRole(RoleId roleId, IReadOnlyCollection<BranchId> branchIds)
    {
        _roles.RemoveAll(r => r.RoleId == roleId);
        _roles.Add(new UserRoleAssignment(roleId, branchIds));
    }

    public void RemoveRole(RoleId roleId) => _roles.RemoveAll(r => r.RoleId == roleId);

    public PermissionOverride AddOverride(string permission, OverrideEffect effect, PermissionScope? scope,
        DateTimeOffset? until, string reason, Guid by, DateTimeOffset at)
    {
        var item = new PermissionOverride(permission, effect, scope, until, reason, by, at);
        _overrides.Add(item);
        return item;
    }

    public void RemoveOverride(Guid overrideId) => _overrides.RemoveAll(o => o.Id == overrideId);

    public Result Lock(string reason)
    {
        if (Status == UserAccountStatus.Locked) return UserAccountErrors.AlreadyLocked;
        (Status, LockedReason) = (UserAccountStatus.Locked, reason);
        return Result.Success();
    }

    public void Unlock() => (Status, LockedReason) = (UserAccountStatus.Active, null);

    public void RecordLogin(DateTimeOffset at) => (LastLoginAt, Throttle) = (at, LoginThrottle.None);

    public void EnableTwoFactor(string secret) => (TwoFactorEnabled, TwoFactorSecret) = (true, secret);

    public void DisableTwoFactor() => (TwoFactorEnabled, TwoFactorSecret) = (false, null);
}
