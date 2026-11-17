using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>Lỗi của nhóm đăng nhập & bảo mật (user story 01) — mã trùng tài liệu.</summary>
public static class AuthErrors
{
    /// <summary>AUTH-01.E1 — sai mật khẩu và không tồn tại tài khoản dùng CHUNG 1 thông báo</summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized("auth.invalid_credentials", "Email hoặc mật khẩu không đúng.");
    public static readonly Error RefreshInvalid = Error.Unauthorized("auth.refresh_invalid", "Phiên đăng nhập đã hết, vui lòng đăng nhập lại.");
    /// <summary>2 tab làm mới phiên cùng lúc — tab chậm hơn chỉ cần thử lại, KHÔNG coi là bị đánh cắp</summary>
    public static readonly Error RefreshConflict = Error.Unauthorized("auth.refresh_conflict", "Phiên vừa được làm mới ở tab khác, vui lòng thử lại.");
    public static readonly Error TokenReused = Error.Unauthorized("auth.token_reused", "Phát hiện phiên đăng nhập bất thường. Vui lòng đăng nhập lại.");
    public static readonly Error OtpRequired = Error.Validation("auth.otp_required", "Bạn cần xác thực mã OTP trước.");
    public static readonly Error OtpRateLimited = Error.Conflict("auth.otp_rate_limited", "Bạn đã yêu cầu gửi mã quá nhiều lần. Vui lòng thử lại sau 1 giờ.");
    public static readonly Error WrongPassword = Error.Validation("auth.wrong_password", "Mật khẩu hiện tại không đúng.");
    public static readonly Error SamePassword = Error.Validation("auth.same_password", "Mật khẩu mới phải khác mật khẩu hiện tại.");
    public static readonly Error PasswordLikeLogin = Error.Validation("auth.password_like_login", "Mật khẩu không được trùng email / tên đăng nhập.");
    public static readonly Error InviteInvalid = Error.NotFound("auth.invite_invalid", "Link kích hoạt không còn hiệu lực. Vui lòng nhờ quản trị gửi lại lời mời.");
    public static readonly Error SessionNotFound = Error.NotFound("auth.session_not_found", "Không tìm thấy phiên đăng nhập.");

    public static Error ResendTooSoon(int seconds) =>
        Error.Conflict("auth.otp_resend_too_soon", $"Vui lòng đợi {seconds} giây rồi gửi lại mã.");
}
