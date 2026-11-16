using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

using JPCenter.Modules.System.Domain.Common;

namespace JPCenter.Modules.System.Domain.PortalAccounts;

/// <summary>Tài khoản Cổng học viên / phụ huynh. Bảng: system.portal_accounts</summary>
public sealed class PortalAccount : AggregateRoot<PortalAccountId>, IAuditable
{
    private PortalAccount() { }

    public PortalOwnerType OwnerType { get; private set; }
    /// <summary>Students.Student.Id hoặc Crm.Lead.Id (phụ huynh)</summary>
    public Guid OwnerId { get; private set; }
    public string Username { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public PortalAccountStatus Status { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    /// <summary>Khóa tạm khi nhập sai nhiều lần (owned: failed_login_count, locked_until) — AUTH-11</summary>
    public LoginThrottle Throttle { get; private set; } = LoginThrottle.None;
    /// <summary>Mật khẩu tạm do giáo vụ cấp → buộc đổi ở lần đăng nhập đầu — AUTH-10</summary>
    public bool MustChangePassword { get; private set; }

    public static Result<PortalAccount> Create(PortalOwnerType ownerType, Guid ownerId, string username, string? email)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Error.Validation("system.portal_account.username_required", "Chưa nhập tên đăng nhập");
        return new PortalAccount
        {
            Id = PortalAccountId.New(), OwnerType = ownerType, OwnerId = ownerId, Username = username.Trim().ToLowerInvariant(),
            Email = email, Status = PortalAccountStatus.Active,
        };
    }

    /// <summary>Chủ tài khoản tự đổi / đặt lại mật khẩu</summary>
    public void SetPassword(string passwordHash) => (PasswordHash, MustChangePassword, Throttle) = (passwordHash, false, LoginThrottle.None);

    /// <summary>Giáo vụ cấp / đặt lại mật khẩu tạm</summary>
    public void SetTemporaryPassword(string passwordHash) => (PasswordHash, MustChangePassword) = (passwordHash, true);

    public Result CanSignIn(DateTimeOffset now) =>
        Throttle.EnsureNotLocked(now) is { IsFailure: true } r ? r
        : Status switch
        {
            PortalAccountStatus.Locked => Error.Forbidden("auth.account_locked", "Tài khoản đã bị khóa. Vui lòng liên hệ trung tâm."),
            PortalAccountStatus.Inactive => Error.Forbidden("auth.account_inactive", "Tài khoản đã ngừng hoạt động."),
            _ => Result.Success(),
        };

    public bool RecordFailedLogin(DateTimeOffset now)
    {
        Throttle = Throttle.Fail(now);
        return Throttle.JustLocked(now);
    }
    public void Deactivate() => Status = PortalAccountStatus.Inactive;
    public void Lock() => Status = PortalAccountStatus.Locked;
    public void Activate() => Status = PortalAccountStatus.Active;
    public void RecordLogin(DateTimeOffset at) => (LastLoginAt, Throttle) = (at, LoginThrottle.None);
}
