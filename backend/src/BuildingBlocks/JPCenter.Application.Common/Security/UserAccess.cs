namespace JPCenter.Application.Common.Security;

/// <summary>
/// Ảnh chụp quyền của 1 tài khoản — nạp 1 lần rồi cache (CurrentUserMiddleware), không truy vấn DB mỗi request.
/// Đổi quyền / khóa / đổi mật khẩu → tăng <see cref="TokenVersion"/> + xóa cache (<see cref="IUserAccessCache"/>).
/// </summary>
public sealed record UserAccess(
    Guid AccountId,
    Portal Portal,
    int TokenVersion,
    bool IsActive,
    string DisplayName,
    string RoleName,
    /// <summary>Hr.Employee — chỉ tài khoản nhân viên / GV</summary>
    Guid? EmployeeId,
    /// <summary>Chủ tài khoản cổng: Students.Student (HV) hoặc Crm.Lead (PH)</summary>
    Guid? OwnerId,
    bool IsSuperAdmin,
    /// <summary>Có role gán "mọi chi nhánh"</summary>
    bool AllBranches,
    /// <summary>Chi nhánh được gán (hợp các role)</summary>
    IReadOnlyList<Guid> BranchIds,
    /// <summary>Mã quyền đầy đủ "module.page.action" → phạm vi</summary>
    IReadOnlyDictionary<string, EffectivePermission> Permissions);
