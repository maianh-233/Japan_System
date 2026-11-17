namespace JPCenter.Modules.System.Application.PortalAccounts.Common;

/// <summary>Tài khoản cổng — khớp <c>PortalAccount</c> ở <c>frontend/src/features/student/types.ts</c>.</summary>
/// <param name="OwnerType">"student" · "guardian"</param>
/// <param name="Status">"active" · "inactive" · "locked"</param>
public sealed record PortalAccountDto(Guid Id, string OwnerType, Guid OwnerId, string DisplayName, string Username, string? Email,
    string Status, bool MustChangePassword, DateTimeOffset? LastLoginAt);

/// <summary>Kết quả cấp / đặt lại mật khẩu tạm.</summary>
/// <param name="TemporaryPassword">Chỉ trả khi KHÔNG gửi được email (chưa có email) — giáo vụ đưa trực tiếp cho HV / PH. Hiện 1 lần.</param>
public sealed record PortalCredentialDto(PortalAccountDto Account, string? TemporaryPassword, bool EmailSent);
