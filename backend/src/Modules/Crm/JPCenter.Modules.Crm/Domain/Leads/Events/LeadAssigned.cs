using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Crm.Domain.Leads.Events;

/// <summary>Lead được giao cho Sale (chia tự động, chọn tay, chuyển tay, tự chuyển do SLA) → báo Sale mới.</summary>
public sealed record LeadAssigned(LeadId LeadId, Guid SaleId, Guid? PreviousSaleId) : IDomainEvent;
