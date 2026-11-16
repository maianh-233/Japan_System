using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Sessions;

public static class SessionErrors
{
    public static readonly Error Expired = Error.Validation("auth.session_expired", "Phiên đăng nhập đã hết, vui lòng đăng nhập lại");
    public static readonly Error TokenReused = Error.Forbidden("auth.token_reused", "Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại");
}
