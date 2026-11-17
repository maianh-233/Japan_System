namespace JPCenter.Modules.System.Application.Users.Common;

/// <summary>Tài khoản nhân viên + vai trò + quyền lẻ — khớp <c>UserAccess</c> ở <c>frontend/src/features/system/types.ts</c>.</summary>
/// <param name="AccountId">system.user_accounts — dùng trong đường dẫn API</param>
/// <param name="UserId">= Hr.Employee.id (frontend gọi là userId)</param>
/// <param name="Status">"invited" · "active" · "locked"</param>
public sealed record UserAccessDto(
    Guid AccountId,
    Guid UserId,
    string Name,
    string? Code,
    string? Position,
    Guid? BranchId,
    string Email,
    string Status,
    IReadOnlyList<RoleAssignmentDto> Roles,
    IReadOnlyList<PermOverrideDto> Overrides,
    DateTimeOffset? LastLoginAt,
    string? LockedReason);

/// <param name="BranchIds">Rỗng = mọi chi nhánh</param>
public sealed record RoleAssignmentDto(Guid RoleId, IReadOnlyList<Guid> BranchIds);

/// <param name="Perm">"crm.leads.export"</param>
/// <param name="Effect">"grant" · "deny"</param>
/// <param name="By">Tên người cấp</param>
/// <param name="Expired">Đã hết hạn — không còn tính vào quyền hiệu lực (PQ-07.E3)</param>
public sealed record PermOverrideDto(Guid Id, string Perm, string Effect, string? Scope, DateTimeOffset? Until, string Reason,
    string By, DateTimeOffset At, bool Expired);

/// <summary>Kết quả thay đổi quyền của 1 người.</summary>
/// <param name="PendingApprovalId">Có thêm quyền tài chính → chưa áp dụng, đã gửi yêu cầu duyệt 2 người (PQ-09)</param>
/// <param name="Warnings">Cảnh báo không chặn (vd "quyền đã có từ role X" — PQ-06.E6)</param>
public sealed record UserChangeResult(UserAccessDto User, Guid? PendingApprovalId, string? Message, IReadOnlyList<string> Warnings);
