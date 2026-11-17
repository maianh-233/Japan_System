using System.Text.Json;

namespace JPCenter.Modules.System.Application.Approvals.Common;

/// <summary>Yêu cầu duyệt 2 người — khớp <c>ApprovalRequest</c> ở <c>frontend/src/features/system/approvals.ts</c>.</summary>
/// <param name="Kind">"refund" · "void_invoice" · "finance_permission"</param>
/// <param name="Status">"pending" · "approved" · "rejected" · "withdrawn"</param>
/// <param name="CanDecide">Người đang xem duyệt / từ chối được (không phải người tạo, đủ quyền theo loại)</param>
/// <param name="CanWithdraw">Người đang xem là người tạo và yêu cầu còn chờ</param>
public sealed record ApprovalDto(
    Guid Id,
    string Code,
    string Kind,
    string Title,
    string? Detail,
    Guid? BranchId,
    JsonElement Payload,
    PersonDto RequestedBy,
    DateTimeOffset RequestedAt,
    string Status,
    PersonDto? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Note,
    bool CanDecide,
    bool CanWithdraw);

public sealed record PersonDto(Guid Id, string Name);

/// <summary>Kết quả duyệt — kèm cảnh báo (vd người được cấp đang bị khóa — PQ-09.E5).</summary>
public sealed record ApprovalDecisionResult(ApprovalDto Request, IReadOnlyList<string> Warnings);
