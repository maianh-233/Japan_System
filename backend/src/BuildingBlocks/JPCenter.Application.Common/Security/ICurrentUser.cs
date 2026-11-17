namespace JPCenter.Application.Common.Security;

/// <summary>
/// Người đang gọi request. Ngoài HTTP (job nền, nạp dữ liệu mẫu) → chưa đăng nhập, <see cref="AccountId"/> = null (= hệ thống).
/// Giai đoạn 2 bổ sung cổng, chi nhánh, quyền hiệu lực.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id tài khoản (user_accounts / portal_accounts) — dùng cho created_by, updated_by, audit.</summary>
    Guid? AccountId { get; }
}
