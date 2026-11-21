using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Hóa đơn hết nợ (đóng đủ, hoặc phải thu = 0 ngay khi lập — TC-04.E3).</summary>
public sealed record InvoicePaid(InvoiceId InvoiceId, Guid StudentId, BranchId BranchId) : IDomainEvent;
