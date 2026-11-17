using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.System.Domain.Sessions;

namespace JPCenter.Modules.System.Domain.OneTimeTokens;

/// <summary>
/// Mã / link dùng 1 lần: link mời, OTP quên mật khẩu, vé đặt mật khẩu mới. Chỉ lưu bản băm.
/// Tạo mã mới cùng mục đích → mã cũ của tài khoản đó vô hiệu (Application gọi Invalidate).
/// User story AUTH-04, 05. Bảng: system.one_time_tokens
/// </summary>
public sealed class OneTimeToken : AggregateRoot<OneTimeTokenId>
{
    private OneTimeToken() { }

    public TokenPurpose Purpose { get; private set; }
    public AccountKind AccountKind { get; private set; }
    /// <summary>Tài khoản sở hữu — null khi email không tồn tại (vẫn tạo bản ghi giả để phản hồi giống hệt, chống dò email)</summary>
    public Guid? AccountId { get; private set; }
    /// <summary>Email / SĐT đã nhập (chuẩn hóa) — để giới hạn số lần gửi theo người nhận</summary>
    public string Recipient { get; private set; } = null!;
    /// <summary>Băm của mã / token</summary>
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    /// <summary>Bị thay bởi mã mới / hủy</summary>
    public DateTimeOffset? InvalidatedAt { get; private set; }

    public static OneTimeToken Issue(TokenPurpose purpose, AccountKind kind, Guid? accountId, string recipient, string tokenHash,
        DateTimeOffset now, TimeSpan lifetime, int maxAttempts = 5) =>
        new()
        {
            Id = OneTimeTokenId.New(), Purpose = purpose, AccountKind = kind, AccountId = accountId, Recipient = recipient, TokenHash = tokenHash,
            CreatedAt = now, ExpiresAt = now + lifetime, MaxAttempts = maxAttempts,
        };

    /// <summary>Kiểm tra mã. Đúng → đánh dấu đã dùng. Sai → tăng lượt, hết lượt thì khóa.</summary>
    public Result Verify(string tokenHash, DateTimeOffset now)
    {
        if (UsedAt is not null || InvalidatedAt is not null) return OneTimeTokenErrors.Expired;
        if (now > ExpiresAt) return OneTimeTokenErrors.Expired;
        if (Attempts >= MaxAttempts) return OneTimeTokenErrors.Locked;
        // Mã giả (email không tồn tại) không bao giờ đúng
        if (AccountId is null || !string.Equals(tokenHash, TokenHash, StringComparison.Ordinal))
        {
            Attempts++;
            var left = MaxAttempts - Attempts;
            return left > 0
                ? Error.Validation("auth.otp_invalid", $"Mã không đúng. Còn {left} lần thử.")
                : OneTimeTokenErrors.Locked;
        }
        UsedAt = now;
        return Result.Success();
    }

    public void Invalidate(DateTimeOffset now) => InvalidatedAt ??= now;
}
