using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Permissions.Common;

/// <summary>
/// Quy tắc vàng 1 (user story 02): chỉ cấp được quyền MÌNH ĐANG CÓ, phạm vi KHÔNG RỘNG HƠN của mình (Super Admin cấp được mọi thứ).
/// Giống <c>access.ts › canGrant</c>. Dùng cho người đang thao tác và cho người đề nghị lúc duyệt (PQ-09.E4).
/// </summary>
public sealed class GrantChecker(UserAccess granter)
{
    public bool IsSuperAdmin => granter.IsSuperAdmin;

    public bool CanGrant(string permission, PermissionScope scope) =>
        granter.IsSuperAdmin || (granter.Permissions.TryGetValue(permission, out var mine) && mine.Scope >= scope);

    /// <summary>Các mã quyền trong <paramref name="grants"/> vượt quá quyền người cấp.</summary>
    public IReadOnlyList<string> Exceeding(IReadOnlyDictionary<string, PermissionScope> grants) =>
        grants.Where(g => !CanGrant(g.Key, g.Value)).Select(g => g.Key).ToList();

    /// <summary>PQ-05.E5: người cấp phạm vi chi nhánh chỉ chọn chi nhánh mình được gán, không chọn "mọi chi nhánh" (rỗng).</summary>
    public bool CanUseBranches(IReadOnlyCollection<Guid> branchIds) =>
        granter.IsSuperAdmin || granter.AllBranches || (branchIds.Count > 0 && branchIds.All(granter.BranchIds.Contains));
}
