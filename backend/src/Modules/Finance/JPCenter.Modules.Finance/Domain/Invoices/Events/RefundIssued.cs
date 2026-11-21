using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Đã hoàn tiền (TC-07) → báo HV / PH, trừ thực thu.</summary>
public sealed record RefundIssued(InvoiceId InvoiceId, RefundId RefundId, Guid StudentId, BranchId BranchId, Money Amount, DateTimeOffset At) : IDomainEvent;
