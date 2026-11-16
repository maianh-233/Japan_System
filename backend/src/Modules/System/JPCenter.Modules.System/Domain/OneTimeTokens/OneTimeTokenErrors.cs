using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.OneTimeTokens;

public static class OneTimeTokenErrors
{
    public static readonly Error Expired = Error.Validation("auth.otp_expired", "Mã đã hết hạn hoặc đã dùng. Vui lòng gửi lại.");
    public static readonly Error Locked = Error.Validation("auth.otp_locked", "Nhập sai quá nhiều lần. Vui lòng gửi lại mã mới.");
}
