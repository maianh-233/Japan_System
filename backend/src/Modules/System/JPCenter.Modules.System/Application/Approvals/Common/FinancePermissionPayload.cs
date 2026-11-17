namespace JPCenter.Modules.System.Application.Approvals.Common;

/// <summary>
/// Nội dung yêu cầu cấp quyền tài chính chờ duyệt (approval_requests.payload, kind = finance_permission) — áp dụng ĐÚNG như vậy khi duyệt.
/// </summary>
/// <param name="Type">"user_roles" (gán vai trò) · "user_override" (cấp lẻ) · "role_grants" (sửa ma trận role đang có người dùng)</param>
/// <param name="FinanceAdded">Quyền tài chính sẽ có thêm — hiển thị cho người duyệt</param>
public sealed record FinancePermissionPayload(
    string Type,
    Guid? AccountId,
    string? UserName,
    List<RoleAssignmentInput>? Roles,
    OverrideInput? Override,
    Guid? RoleId,
    string? RoleName,
    Dictionary<string, string>? Grants,
    List<string> FinanceAdded,
    string? Before,
    string? After)
{
    public const string UserRoles = "user_roles";
    public const string UserOverride = "user_override";
    public const string RoleGrants = "role_grants";
}

/// <summary>1 dòng gán vai trò — rỗng chi nhánh = mọi chi nhánh.</summary>
public sealed record RoleAssignmentInput(Guid RoleId, List<Guid> BranchIds);

/// <param name="Effect">"grant" · "deny"</param>
/// <param name="Scope">"own" · "branch" · "all" (chỉ khi grant)</param>
public sealed record OverrideInput(string Permission, string Effect, string? Scope, DateTimeOffset? Until, string Reason);
