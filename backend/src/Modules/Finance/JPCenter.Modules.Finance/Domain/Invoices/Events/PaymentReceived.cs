using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Đã thu tiền → hoa hồng Sale, báo cáo doanh thu, cảnh báo bỏ học (hết nợ).</summary>
public sealed record PaymentReceived(InvoiceId InvoiceId, PaymentId PaymentId, Guid StudentId, BranchId BranchId, Money Amount, Guid CollectorId,
    DateTimeOffset At) : IDomainEvent;
