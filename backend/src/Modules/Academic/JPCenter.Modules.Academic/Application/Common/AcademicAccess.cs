using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Common;

/// <summary>
/// Phạm vi quyền cho lớp, buổi, phòng (PQ-10): <c>all</c> mọi chi nhánh · <c>branch</c> chi nhánh được gán ·
/// <c>own</c> chi nhánh được gán VÀ chỉ lớp mình là GV / trợ giảng (DT-01.E1).
/// Ngoài phạm vi → coi như không tồn tại (404).
/// </summary>
public sealed class AcademicAccess(ICurrentUser user)
{
    public const string Classes = "academic.classes";
    public const string Sessions = "academic.sessions";
    public const string Rooms = "academic.rooms";
    /// <summary>Đơn chờ xử lý: đơn hủy buổi của GV, đơn xin nghỉ của HV / PH, mời dạy thay (<c>approve</c> = duyệt)</summary>
    public const string Requests = "academic.requests";

    /// <summary>Người thao tác để ghi vào dữ liệu (xác nhận, hủy...): nhân viên (Hr) nếu có, không thì tài khoản</summary>
    public Guid ActorId => user.Access?.EmployeeId ?? user.AccountId ?? Guid.Empty;

    public string ActorName => user.Access?.DisplayName ?? "Hệ thống";

    public bool Can(string page, PermissionAction action) => user.Can(page, action);

    /// <summary>Chi nhánh được thao tác; null = mọi chi nhánh; rỗng = không có quyền</summary>
    public HashSet<Guid>? Branches(string page, PermissionAction action)
    {
        var perm = user.Permission(page, action);
        if (perm is null) return [];
        if (perm.Scope == PermissionScope.All) return null;
        if (perm.BranchIds.Count > 0) return perm.BranchIds.ToHashSet();
        var me = user.Access;
        return me is null || me.AllBranches ? null : me.BranchIds.ToHashSet();
    }

    /// <summary>Phạm vi own: chỉ lớp / buổi mình dạy — trả Id nhân viên để lọc; null = không giới hạn theo người</summary>
    public Guid? OwnOnly(string page, PermissionAction action) =>
        user.Permission(page, action) is { Scope: PermissionScope.Own } ? user.Access?.EmployeeId ?? Guid.Empty : null;

    public bool InBranch(string page, PermissionAction action, Guid branchId) =>
        Branches(page, action) is not { } allowed || allowed.Contains(branchId);

    public static readonly Error BranchNotAllowed = Error.Forbidden("academic.branch_not_allowed",
        "Bạn chỉ thao tác được dữ liệu của chi nhánh mình được phân công");
}
