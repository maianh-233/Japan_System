using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Đã thu tiền (TC-06 / TC-10) → báo HV / PH, doanh thu, tiền mặt ca của người thu.</summary>
public sealed record PaymentRecorded(InvoiceId InvoiceId, PaymentId PaymentId, Guid StudentId, BranchId BranchId, Money Amount, PaymentMethod Method,
    Guid? CollectorId, DateTimeOffset At) : IDomainEvent;
