namespace JPCenter.Modules.System.Application.Roles.Common;

/// <summary>Vai trò — khớp <c>Role</c> ở <c>frontend/src/features/system/types.ts</c>.</summary>
/// <param name="Grants">"crm.leads.update" → "own" | "branch" | "all" (Super Admin: rỗng, xem <see cref="SuperAdmin"/>)</param>
/// <param name="UserCount">Số người đang dùng — không tính tài khoản bị khóa (PQ-01)</param>
/// <param name="Version">Gửi lại khi lưu ma trận — người khác vừa sửa thì báo tải lại (PQ-03.E6)</param>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool System,
    bool SuperAdmin,
    IReadOnlyDictionary<string, string> Grants,
    int UserCount,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    uint Version);

/// <summary>Kết quả lưu role.</summary>
/// <param name="KeptCells">Ô quyền vượt quá quyền người sửa → giữ nguyên / không sao chép (PQ-02.E2, PQ-03.E2)</param>
/// <param name="PendingApprovalId">Phần thêm quyền tài chính cho role đang có người dùng → chờ duyệt 2 người (PQ-03.E5)</param>
public sealed record RoleSaveResult(RoleDto Role, IReadOnlyList<string> KeptCells, Guid? PendingApprovalId, string? Message);
