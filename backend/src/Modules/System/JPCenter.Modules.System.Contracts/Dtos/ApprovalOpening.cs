namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Phiếu duyệt module khác mở.</summary>
/// <param name="Kind">"refund" · "void_invoice"</param>
/// <param name="Payload">Đối tượng nội dung (ghi jsonb, frontend đọc theo Kind) — vd <c>{ kind, invoiceId, invoiceCode, amount, method, reason }</c></param>
public sealed record ApprovalOpening(string Kind, string Title, string? Detail, Guid? BranchId, object Payload);
