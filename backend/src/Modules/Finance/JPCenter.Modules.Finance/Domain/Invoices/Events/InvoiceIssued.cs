using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Hóa đơn mới → Notifications gửi phụ huynh nhận hóa đơn.</summary>
public sealed record InvoiceIssued(InvoiceId InvoiceId, Guid StudentId, BranchId BranchId, Money NetAmount) : IDomainEvent;
