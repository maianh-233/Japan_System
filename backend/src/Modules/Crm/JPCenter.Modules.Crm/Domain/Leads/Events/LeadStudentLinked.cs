using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Crm.Domain.Leads.Events;

/// <summary>Lead được liên kết với học viên (CRM-07, CRM-08) → System cấp quyền cổng PH xem HV đó.</summary>
public sealed record LeadStudentLinked(LeadId LeadId, Guid StudentId) : IDomainEvent;
