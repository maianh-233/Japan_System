using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Users;

/// <summary>Gán role cho người dùng theo chi nhánh. Bảng: system.user_role_assignments</summary>
public sealed class UserRoleAssignment
{
    private UserRoleAssignment() { }

    internal UserRoleAssignment(RoleId roleId, IReadOnlyCollection<BranchId> branchIds) =>
        (RoleId, BranchIds) = (roleId, branchIds.ToList());

    public RoleId RoleId { get; private set; }
    /// <summary>Chi nhánh áp dụng — rỗng = mọi chi nhánh</summary>
    public List<BranchId> BranchIds { get; private set; } = [];
}
