namespace JPCenter.Modules.Finance.Application.Invoices.Common;

/// <summary>Kết quả hoàn tiền / đề nghị hủy: hóa đơn sau thao tác + phiếu chờ duyệt (null = đã ghi ngay, không cần duyệt).</summary>
public sealed record InvoiceActionResult(InvoiceDto Invoice, ApprovalRef? Approval);

/// <summary>Phiếu ở hộp duyệt chung (System.ApprovalRequest)</summary>
public sealed record ApprovalRef(Guid Id, string Code);
