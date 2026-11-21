using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Crm.Domain.Leads.Events;

/// <summary>Gỡ liên kết lead ↔ học viên (CRM-08) → PH mất quyền xem HV đó ngay (CRM-08.E5).</summary>
public sealed record LeadStudentUnlinked(LeadId LeadId, Guid StudentId) : IDomainEvent;
