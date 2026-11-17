using FluentValidation;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>Mật khẩu mới (AUTH-04.E8): tối thiểu 8 ký tự, có chữ và số.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Chưa nhập mật khẩu mới")
            .MinimumLength(MinLength).WithMessage($"Mật khẩu tối thiểu {MinLength} ký tự")
            .MaximumLength(128).WithMessage("Mật khẩu tối đa 128 ký tự")
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit)).WithMessage("Mật khẩu phải có cả chữ và số");

    /// <summary>Không trùng email / tên đăng nhập (so phần trước @, không phân biệt hoa thường).</summary>
    public static bool LooksLikeLogin(string password, string loginName)
    {
        var p = password.Trim().ToLowerInvariant();
        var login = loginName.Trim().ToLowerInvariant();
        return p == login || p == login.Split('@')[0];
    }
}
