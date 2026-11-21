using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Hủy phần học phí còn lại (HV nghỉ / chuyển lớp / lớp hủy) — hết công nợ.</summary>
public sealed record InvoiceCancelled(InvoiceId InvoiceId, Guid StudentId, BranchId BranchId, string Reason) : IDomainEvent;
