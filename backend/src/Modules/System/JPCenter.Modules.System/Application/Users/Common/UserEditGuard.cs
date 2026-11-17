using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Users.Common;

/// <summary>
/// Kiểm tra chung trước khi sửa quyền 1 người: tồn tại, không phải chính mình (PQ-05.E1, PQ-08.E4),
/// vai trò chọn không vượt quá người gán (PQ-05.E2, PQ-08.E1), chi nhánh trong phạm vi người gán (PQ-05.E5).
/// Dùng cả lúc lưu thẳng và lúc người thứ 2 duyệt (kiểm tra lại theo quyền HIỆN TẠI của người đề nghị — PQ-09.E4).
/// </summary>
public sealed class UserEditGuard(ISystemDbContext db, IPermissionCatalog catalog)
{
    public async Task<Result<UserAccount>> LoadTargetAsync(Guid accountId, UserAccess editor, CancellationToken ct)
    {
        var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.Id == new UserAccountId(accountId), ct);
        if (account is null) return PermissionErrors.UserNotFound;
        if (account.Id.Value == editor.AccountId) return PermissionErrors.SelfEdit;
        return account;
    }

    /// <summary>
    /// Kiểm tra các dòng gán vai trò. Chỉ xét dòng THAY ĐỔI so với hiện tại (thêm / đổi chi nhánh / gỡ) —
    /// dòng giữ nguyên không bắt người gán phải đủ quyền.
    /// </summary>
    public Result CheckRoles(IReadOnlyCollection<UserRoleAssignment> current, IReadOnlyCollection<UserRoleAssignment> next,
        IReadOnlyDictionary<RoleId, Role> roles, UserAccess editor)
    {
        var checker = new GrantChecker(editor);
        static string Key(UserRoleAssignment a) => a.RoleId + ":" + string.Join(',', a.BranchIds.Select(b => b.Value).Order());
        var currentKeys = current.Select(Key).ToHashSet();
        var nextKeys = next.Select(Key).ToHashSet();

        foreach (var a in next.Where(a => !currentKeys.Contains(Key(a))).Concat(current.Where(a => !nextKeys.Contains(Key(a)))))
        {
            if (!roles.TryGetValue(a.RoleId, out var role)) return PermissionErrors.RoleNotFound;
            if (role.IsSuperAdmin && !checker.IsSuperAdmin) return PermissionErrors.ExceedsGranter;
            if (checker.Exceeding(RoleGrants.Of(role, catalog)).Count > 0) return PermissionErrors.ExceedsGranter;
            if (!checker.CanUseBranches(a.BranchIds.Select(b => b.Value).ToList())) return PermissionErrors.BranchNotAllowed;
        }
        return Result.Success();
    }

    /// <summary>Kiểm tra 1 quyền lẻ: mã hợp lệ, cấp thì không vượt người cấp (PQ-06.E1); chặn thì luôn được (E2).</summary>
    public Result CheckOverride(OverrideInput input, UserAccess editor, DateTimeOffset now)
    {
        if (!catalog.IsValid(input.Permission)) return PermissionErrors.UnknownPermission;
        if (input.Until is { } until && until <= now) return PermissionErrors.InvalidUntil;
        if (input.Effect == "grant" && !new GrantChecker(editor).CanGrant(input.Permission, RoleGrants.ParseScope(input.Scope ?? "own")))
            return PermissionErrors.ExceedsGranter;
        return Result.Success();
    }

    /// <summary>Tóm tắt vai trò để hiển thị trước → sau: "Kế toán @ Cầu Giấy, Sale @ mọi chi nhánh".</summary>
    public async Task<string> DescribeAsync(IEnumerable<UserRoleAssignment> assignments, IReadOnlyDictionary<RoleId, Role> roles, CancellationToken ct)
    {
        var list = assignments.ToList();
        if (list.Count == 0) return "(không có vai trò)";
        var branchIds = list.SelectMany(a => a.BranchIds).Distinct().ToList();
        var branches = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return string.Join(", ", list.Select(a =>
            $"{(roles.TryGetValue(a.RoleId, out var r) ? r.Name : "?")} @ {(a.BranchIds.Count == 0 ? "mọi chi nhánh" : string.Join(" + ", a.BranchIds.Select(b => branches.GetValueOrDefault(b, "?"))))}"));
    }
}
