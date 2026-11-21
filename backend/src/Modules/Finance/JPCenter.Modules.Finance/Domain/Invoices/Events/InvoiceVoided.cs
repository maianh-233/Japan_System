using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices.Events;

/// <summary>Hóa đơn lập sai đã hủy (TC-08) → Thùng rác, trả lượt ưu đãi.</summary>
public sealed record InvoiceVoided(InvoiceId InvoiceId, Guid StudentId, BranchId BranchId, string Reason) : IDomainEvent;
