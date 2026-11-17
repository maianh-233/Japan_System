using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Security;

/// <summary>
/// 1 quyền đang dùng được của người dùng (đã cộng role, cấp lẻ, trừ chặn lẻ — user story PQ-07).
/// <see cref="BranchIds"/>: chi nhánh áp dụng khi <see cref="Scope"/> = Branch (chi nhánh của các role chứa quyền này).
/// </summary>
public sealed record EffectivePermission(PermissionScope Scope, IReadOnlyList<Guid> BranchIds);
