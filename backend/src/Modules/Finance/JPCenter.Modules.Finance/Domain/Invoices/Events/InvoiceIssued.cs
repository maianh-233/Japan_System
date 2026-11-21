using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Hóa đơn mới (TC-04) → báo người nhận hóa đơn kèm link cổng / VietQR.</summary>
public sealed record InvoiceIssued(InvoiceId InvoiceId, Guid EnrollmentId, Guid StudentId, BranchId BranchId, Money Total, DateOnly DueDate) : IDomainEvent;
