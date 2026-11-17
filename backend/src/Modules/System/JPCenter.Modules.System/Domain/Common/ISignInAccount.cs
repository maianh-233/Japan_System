using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Common;

/// <summary>
/// Tài khoản đăng nhập được — nhân viên (<c>UserAccount</c>) hoặc cổng HV / PH (<c>PortalAccount</c>).
/// Cho đăng nhập, quên / đổi mật khẩu, phiên dùng chung 1 luồng (user story 01).
/// </summary>
public interface ISignInAccount
{
    Guid AccountGuid { get; }
    AccountKind Kind { get; }
    /// <summary>Email / tên đăng nhập — đích của nhật ký đăng nhập</summary>
    string LoginName { get; }
    string? Email { get; }
    string? PasswordHash { get; }
    bool MustChangePassword { get; }
    int TokenVersion { get; }

    /// <summary>Khóa tạm / trạng thái — gọi TRƯỚC khi so mật khẩu (AUTH-11.E1)</summary>
    Result CheckThrottle(DateTimeOffset now);

    /// <summary>Trạng thái tài khoản — chỉ báo khi mật khẩu ĐÚNG (AUTH-01.E2)</summary>
    Result CheckStatus();

    bool RecordFailedLogin(DateTimeOffset now);
    void RecordLogin(DateTimeOffset at);
    void SetPassword(string passwordHash);
    void RevokeTokens();
}
