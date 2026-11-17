namespace JPCenter.Application.Common.Security;

/// <summary>
/// Người đang gọi request. Ngoài HTTP (job nền, nạp dữ liệu mẫu) → chưa đăng nhập, <see cref="AccountId"/> = null (= hệ thống).
/// Kiểm tra quyền: <see cref="CurrentUserExtensions"/>.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id tài khoản (user_accounts / portal_accounts) — dùng cho created_by, updated_by, audit.</summary>
    Guid? AccountId { get; }

    /// <summary>Id phiên đăng nhập (system.user_sessions) — biết "thiết bị này" (AUTH-08).</summary>
    Guid? SessionId { get; }

    /// <summary>Quyền + thông tin tài khoản; null khi chưa đăng nhập.</summary>
    UserAccess? Access { get; }
}
