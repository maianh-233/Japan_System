namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>
/// Người đang đăng nhập — khớp <c>SessionUser</c> ở <c>frontend/src/types/auth.ts</c> (+ vài trường mới cho giai đoạn nối frontend).
/// </summary>
/// <param name="Id">Nhân viên: Id hồ sơ Hr (frontend tra quyền theo Id nhân viên); cổng: Id tài khoản cổng.</param>
/// <param name="Portal">"staff" · "teacher" · "student" · "parent"</param>
/// <param name="Scope">"global" (có role mọi chi nhánh / Super Admin) · "branch"</param>
/// <param name="Permissions">Mã menu dùng được ("crm.leads" = có quyền Xem); Super Admin = ["*"]; cổng: ["teacher.*"]...</param>
/// <param name="Grants">Mã quyền đầy đủ → phạm vi ("crm.leads.update": "branch") — để ẩn / hiện nút theo hành động.</param>
/// <param name="AvailablePortals">Các cổng được vào (GV kiêm nhân viên — AUTH-01.E8).</param>
public sealed record SessionUserDto(
    Guid Id,
    Guid AccountId,
    string Email,
    string FullName,
    string? AvatarUrl,
    string Portal,
    string RoleName,
    string Scope,
    IReadOnlyList<Guid> BranchIds,
    Guid? PortalAccountId,
    Guid? PortalOwnerId,
    IReadOnlyList<string> Permissions,
    IReadOnlyDictionary<string, string> Grants,
    IReadOnlyList<string> AvailablePortals,
    bool MustChangePassword);
