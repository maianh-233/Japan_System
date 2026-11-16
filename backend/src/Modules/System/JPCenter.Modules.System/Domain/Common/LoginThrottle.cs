using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Common;

/// <summary>
/// Chống dò mật khẩu (user story AUTH-11): sai MaxFailures lần liên tiếp → khóa tạm LockMinutes phút.
/// Owned trong user_accounts và portal_accounts (cột failed_login_count, locked_until). Khác với khóa bởi quản trị.
/// </summary>
public sealed record LoginThrottle(int FailedCount, DateTimeOffset? LockedUntil)
{
    public const int MaxFailures = 5;
    public const int LockMinutes = 15;

    public static readonly LoginThrottle None = new(0, null);

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public Result EnsureNotLocked(DateTimeOffset now) =>
        IsLocked(now)
            ? Error.Conflict("auth.too_many_attempts", $"Bạn đã nhập sai quá nhiều lần, thử lại sau {Math.Ceiling((LockedUntil!.Value - now).TotalMinutes)} phút")
            : Result.Success();

    /// <summary>Ghi 1 lần sai; đủ ngưỡng thì khóa tạm và đặt lại bộ đếm</summary>
    public LoginThrottle Fail(DateTimeOffset now)
    {
        var count = IsLocked(now) ? FailedCount : FailedCount + 1;
        return count >= MaxFailures ? new LoginThrottle(0, now.AddMinutes(LockMinutes)) : new LoginThrottle(count, LockedUntil);
    }

    /// <summary>Vừa bị khóa tạm sau lần sai này?</summary>
    public bool JustLocked(DateTimeOffset now) => FailedCount == 0 && IsLocked(now);
}
