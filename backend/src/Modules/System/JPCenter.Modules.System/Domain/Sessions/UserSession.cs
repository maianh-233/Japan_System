using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Sessions;

/// <summary>
/// Phiên đăng nhập trên 1 thiết bị — giữ refresh token (băm), xoay vòng mỗi lần làm mới.
/// Dùng cho: ghi nhớ đăng nhập, danh sách thiết bị, đăng xuất từ xa.
/// User story AUTH-02, 03, 08. Bảng: system.user_sessions
/// </summary>
public sealed class UserSession : AggregateRoot<UserSessionId>
{
    private UserSession() { }

    public AccountKind AccountKind { get; private set; }
    /// <summary>system.user_accounts.id hoặc system.portal_accounts.id</summary>
    public Guid AccountId { get; private set; }
    /// <summary>Băm SHA-256 của refresh token hiện tại — UQ</summary>
    public string RefreshTokenHash { get; private set; } = null!;
    /// <summary>Băm của token ngay trước (đã xoay vòng) — bị dùng lại = nghi bị đánh cắp</summary>
    public string? PreviousTokenHash { get; private set; }
    /// <summary>Tên thiết bị / trình duyệt, vd "Chrome · Windows"</summary>
    public string Device { get; private set; } = null!;
    public string Ip { get; private set; } = null!;
    /// <summary>Có tích "Ghi nhớ đăng nhập"</summary>
    public bool Remember { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    /// <summary>Lý do thu hồi: logout, logout_others, password_changed, account_locked, token_reuse, admin</summary>
    public string? RevokeReason { get; private set; }

    public static UserSession Start(AccountKind kind, Guid accountId, string refreshTokenHash, string device, string ip, bool remember,
        DateTimeOffset now, DateTimeOffset expiresAt) =>
        new()
        {
            Id = UserSessionId.New(), AccountKind = kind, AccountId = accountId, RefreshTokenHash = refreshTokenHash, Device = device, Ip = ip,
            Remember = remember, CreatedAt = now, LastSeenAt = now, ExpiresAt = expiresAt,
        };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>Làm mới: token cũ thành Previous, token mới thay vào</summary>
    public Result Rotate(string newTokenHash, string ip, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (!IsActive(now)) return SessionErrors.Expired;
        (PreviousTokenHash, RefreshTokenHash, Ip, LastSeenAt, ExpiresAt) = (RefreshTokenHash, newTokenHash, ip, now, expiresAt);
        return Result.Success();
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        (RevokedAt, RevokeReason) = (now, reason);
    }
}
