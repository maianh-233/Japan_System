using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Leads.Events;

/// <summary>Có lead mới → chạy quy tắc chia lead, gửi tin chào.</summary>
public sealed record LeadCreated(LeadId LeadId, BranchId BranchId, LeadSource Source) : IDomainEvent;
